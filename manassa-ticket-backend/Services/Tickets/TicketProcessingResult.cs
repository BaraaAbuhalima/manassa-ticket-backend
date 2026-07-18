using manassa_ticket_backend.Models;

namespace manassa_ticket_backend.Services.Tickets;

public class TicketProcessingResult
{
    public required bool Success { get; init; }
    public Ticket? Ticket { get; init; }
    public string? RejectionReason { get; init; }
}
