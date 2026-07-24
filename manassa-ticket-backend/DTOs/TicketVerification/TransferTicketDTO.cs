namespace manassa_ticket_backend.DTOs.TicketVerification;

public class TransferTicketDTO
{
    public required TicketDetailsWrapper Details { get; set; }
}

public class TicketDetailsWrapper
{
    public required TicketDataContainer Data { get; set; }
}

public class TicketDataContainer
{
    public required string Name { get; set; }
    public required string Passport_number { get; set; }
    public int Count_luggage { get; set; }
    public required string Ticket_amount { get; set; }
    public required string Barcode { get; set; }
    public required BookingData Booking { get; set; }
}

public class BookingData
{
    public required string Travel_date { get; set; }
    public required string Travel_time_from { get; set; }
}