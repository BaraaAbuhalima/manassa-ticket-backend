using jett_exchange_backend.Messaging;

namespace jett_exchange_backend.Services.Notifications;

public interface ITicketAvailableNotifier
{
    Task NotifySubscribersAsync(TicketAvailableMessage message, CancellationToken cancellationToken = default);
}
