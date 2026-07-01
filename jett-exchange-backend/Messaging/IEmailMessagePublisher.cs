namespace jett_exchange_backend.Messaging;

public interface IEmailMessagePublisher
{
    Task PublishAsync(SendEmailMessage message, CancellationToken cancellationToken = default);
}
