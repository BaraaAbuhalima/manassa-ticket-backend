namespace manassa_ticket_backend.Messaging;

public interface ITicketPurchasedNotificationPublisher
{
    Task PublishAsync(SendEmailMessage message, CancellationToken cancellationToken = default);
}
