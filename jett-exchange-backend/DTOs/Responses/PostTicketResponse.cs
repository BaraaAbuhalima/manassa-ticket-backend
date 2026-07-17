using jett_exchange_backend.Models;

namespace jett_exchange_backend.DTOs.Responses;

public class PostTicketResponse
{
    public required Guid TicketId { get; set; }
    public required string RefPin { get; set; }
    public required TicketSellStatus Status { get; set; }
}