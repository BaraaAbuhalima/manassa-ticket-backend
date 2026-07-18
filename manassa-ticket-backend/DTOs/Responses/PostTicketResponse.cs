using manassa_ticket_backend.Models;

namespace manassa_ticket_backend.DTOs.Responses;

public class PostTicketResponse
{
    public required Guid TicketId { get; set; }
    public required string RefPin { get; set; }
    public required TicketSellStatus Status { get; set; }
}