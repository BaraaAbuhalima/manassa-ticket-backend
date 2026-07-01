namespace jett_exchange_backend.Messaging;

public interface ITicketAvailablePublisher
{
    Task PublishAsync(TicketAvailableMessage message, CancellationToken cancellationToken = default);
}
