using manassa_ticket_backend.Messaging;

namespace manassa_ticket_backend.Services.Notifications;

public interface ITicketAvailableNotifier
{
    Task NotifySubscribersAsync(TicketAvailableMessage message, CancellationToken cancellationToken = default);
}
