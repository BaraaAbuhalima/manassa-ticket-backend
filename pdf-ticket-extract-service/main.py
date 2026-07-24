from fastapi import FastAPI, UploadFile, File
import fitz
import cv2
import numpy as np
import re
from datetime import datetime

app = FastAPI()

# Regex patterns
TICKET_REGEX = re.compile(r"تذكرة\s*[:\-]?\s*(\d+)")
DATE_LABEL = "التاريخ"
TIME_LABEL = "الوقت"
DATE_VALUE_REGEX = re.compile(r"\d{1,2}/\d{1,2}/\d{4}")
TIME_VALUE_REGEX = re.compile(r"\d{1,2}:\d{2}")
PERIOD_REGEX = re.compile(r"AM|PM", re.IGNORECASE)


def extract_ticket(text):
    match = TICKET_REGEX.search(text)
    return match.group(1) if match else None


def _line_containing(text, label):
    for line in text.splitlines():
        if label in line:
            return line
    return None


def extract_datetime(text):
    date_line = _line_containing(text, DATE_LABEL)
    date_match = DATE_VALUE_REGEX.search(date_line) if date_line else None
    if not date_match:
        return None

    # Date and time are printed as separate labeled lines (not "date time" on one
    # line), and the "Time" field is a boarding window, e.g. "12:00 PM - 1:00 PM".
    # The PDF's text layer emits that line with the words reversed
    # ("PM - 1:00 PM 12:00 :الوقت") because the LTR time range sits inside an RTL
    # line with no directional override, so the window's start time - what should
    # represent the ticket's time - ends up as the *last* time token, not the first.
    time_line = _line_containing(text, TIME_LABEL)
    time_values = TIME_VALUE_REGEX.findall(time_line) if time_line else []
    if not time_values:
        return None
    time_str = time_values[-1]

    period_match = PERIOD_REGEX.search(time_line)
    if period_match:
        time_format = "%d/%m/%Y %I:%M %p"
        combined = f"{date_match.group(0)} {time_str} {period_match.group(0).upper()}"
    else:
        time_format = "%d/%m/%Y %H:%M"
        combined = f"{date_match.group(0)} {time_str}"

    try:
        return datetime.strptime(combined, time_format).isoformat()
    except Exception:
        return None


def decode_qr(image):
    detector = cv2.QRCodeDetector()

    data, points, _ = detector.detectAndDecode(image)
    if data:
        return data

    retval, decoded_info, points, _ = detector.detectAndDecodeMulti(image)
    if retval:
        for item in decoded_info:
            if item:
                return item

    return None


def read_ticket(pdf_bytes):
    doc = fitz.open(stream=pdf_bytes, filetype="pdf")

    # Extract all text
    text = ""
    for page in doc:
        text += page.get_text()

    ticket = extract_ticket(text)
    event_datetime = extract_datetime(text)

    qr = None

    # Try extracting QR from embedded images
    for page in doc:
        for img in page.get_images(full=True):
            xref = img[0]
            base = doc.extract_image(xref)

            arr = np.frombuffer(base["image"], np.uint8)
            image = cv2.imdecode(arr, cv2.IMREAD_COLOR)

            if image is None:
                continue

            qr = decode_qr(image)
            if qr:
                break

        if qr:
            break

    # If QR not found, render page and scan it
    if qr is None:
        page = doc.load_page(0)
        pix = page.get_pixmap(matrix=fitz.Matrix(4, 4))

        img = np.frombuffer(pix.samples, dtype=np.uint8)
        img = img.reshape(pix.height, pix.width, pix.n)

        if pix.n == 4:
            img = cv2.cvtColor(img, cv2.COLOR_RGBA2BGR)
        else:
            img = cv2.cvtColor(img, cv2.COLOR_RGB2BGR)

        qr = decode_qr(img)

    doc.close()

    return {
        "ticket": ticket,
        "qrData": qr,
        "dateTime": event_datetime
    }


@app.post("/extract-ticket-pdf-info")
async def process_pdf(file: UploadFile = File(...)):
    pdf_bytes = await file.read()

    result = read_ticket(pdf_bytes)

    return {
        "TicketId": result["ticket"],
        "BarCode": result["qrData"],
        "DateTime": result["dateTime"]
    }