namespace jett_exchange_backend.DTOs.Responses;

public class PostTicketResponse
{
    public required Guid TicketId { get; set; }
    public required string RefPin { get; set; }
}