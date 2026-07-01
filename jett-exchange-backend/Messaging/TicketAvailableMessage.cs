namespace jett_exchange_backend.Messaging;

public class TicketAvailableMessage
{
    public required Guid TicketId { get; set; }
    public required DateOnly Date { get; set; }
}
