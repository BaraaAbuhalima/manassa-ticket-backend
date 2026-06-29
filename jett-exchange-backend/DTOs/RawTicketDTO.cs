namespace jett_exchange_backend.DTOs;

public class RawTicketDTO
{
    public required string TicketId { get; set; }
    public required string OriginalOwnerName { get; set; }
    public required string OriginalOwnerPassportNumber { get; set; }

    public DateTime TicketDateTime { get; set; }

    public required decimal Price { get; set; }
    public required int NumberOfBags { get; set; }
    public required decimal TotalPrice { get; set; }
}