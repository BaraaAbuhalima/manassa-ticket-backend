from fastapi import FastAPI
import os

app = FastAPI()

BASE_DIR = os.path.abspath("Uploads")

@app.post("/extract-ticket-pdf-info/{file_path:path}")
async def process_pdf(file_path: str):

    full_path = os.path.abspath(file_path)

    if not full_path.startswith(BASE_DIR):
        return {"error": "Invalid file path"}

    if not os.path.exists(full_path):
        return {"error": "File not found"}
#make the response of the api like this 
    return {
        "TicketId": self.TicketId,
        "OriginalOwnerName": self.OriginalOwnerName,
        "OriginalOwnerPassportNumber": self.OriginalOwnerPassportNumber,
        "Date": self.Date,
        "Price": self.Price,
        "NumberOfBags": self.NumberOfBags,
        "TotalPrice": self.TotalPrice,
    }