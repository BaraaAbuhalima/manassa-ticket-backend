namespace manassa_ticket_backend.Messaging;

public interface IEmailMessagePublisher
{
    Task PublishAsync(SendEmailMessage message, CancellationToken cancellationToken = default);
}
