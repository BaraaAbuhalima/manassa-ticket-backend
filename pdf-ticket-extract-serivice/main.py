from fastapi import FastAPI
import os

app = FastAPI()

BASE_DIR = os.path.abspath("Uploads")

@app.post("/extract-ticket-pdf-info")
async def process_pdf(file_path: str):

    print(f"Received file path: {file_path}")

    full_path = os.path.abspath(file_path)

    # security check (VERY important)
    # if not full_path.startswith(BASE_DIR):
    #     return {"error": "Invalid file path"}
    # 
    # if not os.path.exists(full_path):
    #     return {"error": "File not found"}

    # 👉 your processing here
    result = {
        "TicketId": "2235325623",
        "BarCode":"123"
    }

    return result