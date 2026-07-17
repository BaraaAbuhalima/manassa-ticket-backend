using jett_exchange_backend.Models;

namespace jett_exchange_backend.Services.Tickets;

public class TicketProcessingResult
{
    public required bool Success { get; init; }
    public Ticket? Ticket { get; init; }
    public string? RejectionReason { get; init; }
}
