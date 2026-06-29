from fastapi import FastAPI
import fitz
import cv2
import numpy as np
import re
import os

app = FastAPI()
TICKET_REGEX = re.compile(r"تذكرة\s*[:\-]?\s*(\d+)")
def extract_ticket(text):
    match = TICKET_REGEX.search(text)
    return match.group(1) if match else None
    
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

def read_ticket(pdf_path):
    doc = fitz.open(pdf_path)
    text = ""
    for page in doc:
        text += page.get_text()
    ticket = extract_ticket(text)
    qr = None
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

    return {
        "ticket": ticket,
        "qrData": qr
    }

@app.post("/extract-ticket-pdf-info")
async def process_pdf(file_path: str):

    full_path = os.path.abspath(file_path)

    if not os.path.exists(full_path):
        return {"error": "File not found"}
    # security check (VERY important)
    # if not full_path.startswith(BASE_DIR):
    #     return {"error": "Invalid file path"}
    # 
    # if not os.path.exists(full_path):
    #     return {"error": "File not found"}

    # 👉 your processing here
    result = read_ticket(full_path)

    return {
        "TicketId": result["ticket"],
        "BarCode": result["qrData"]
    }