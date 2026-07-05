namespace jett_exchange_backend.Messaging;

public interface ITicketSoldNotificationPublisher
{
    Task PublishAsync(SendEmailMessage message, CancellationToken cancellationToken = default);
}
