namespace manassa_ticket_backend.Messaging;

public interface ITicketSoldNotificationPublisher
{
    Task PublishAsync(SendEmailMessage message, CancellationToken cancellationToken = default);
}
