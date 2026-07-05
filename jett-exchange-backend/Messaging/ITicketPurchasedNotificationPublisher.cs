namespace jett_exchange_backend.Messaging;

public interface ITicketPurchasedNotificationPublisher
{
    Task PublishAsync(SendEmailMessage message, CancellationToken cancellationToken = default);
}
