namespace manassa_ticket_backend.DTOs.TicketVerification;

public class TransferTicketDTO
{
    public required string Status { get; set; }
    public required TicketDetailsWrapper Details { get; set; }
}

public class TicketDetailsWrapper
{
    public required string Status { get; set; }
    public required TicketDataContainer Data { get; set; }
}

public class TicketDataContainer
{
    public int Id { get; set; }
    public int Booking_id { get; set; }
    public int Type { get; set; }
    public required string Name { get; set; }
    public required string Passport_number { get; set; }
    public int Count_luggage { get; set; }
    public int Is_additional_luggage { get; set; }
    public int Related_transaction_id { get; set; }
    public required string Ticket_id { get; set; }
    public required string Master_ticket_id { get; set; }
    public required string Ticket_amount { get; set; }
    public required string Barcode { get; set; }
    public required string Ticket_pdf { get; set; }
    public required string Sent_whatsapp_at { get; set; }
    public required string Sent_email_at { get; set; }
    public required string Created_at { get; set; }
    public required string Updated_at { get; set; }
    public required BookingData Booking { get; set; }
}

public class BookingData
{
    public int Id { get; set; }
    public int User_id { get; set; }
    public int Trip_id { get; set; }
    public int Booking_type { get; set; }
    public int Adults { get; set; }
    public required string Payment_ref { get; set; }
    public required string Travel_date { get; set; }
    public required string Travel_time_from { get; set; }
    public required string Travel_time_to { get; set; }
    public required string Booking_date { get; set; }
    public required string User_email { get; set; }
    public required string User_phone { get; set; }
    public required string User_name { get; set; }
    public int Third_party_status { get; set; }
    public required string Created_at { get; set; }
    public required string Updated_at { get; set; }
}