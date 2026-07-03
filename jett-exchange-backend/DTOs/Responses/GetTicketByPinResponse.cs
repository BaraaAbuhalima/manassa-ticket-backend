namespace jett_exchange_backend.DTOs.Responses;

public class GetTicketByPinResponse
{
    public required Guid Id { get; set; }
    public required DateTime TicketDateTime { get; set; }
    public required int NumberOfBags { get; set; }
    public required decimal TotalPrice { get; set; }
    public required string DeleteToken { get; set; }
}
