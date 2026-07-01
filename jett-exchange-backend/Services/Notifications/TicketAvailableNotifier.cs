using jett_exchange_backend.Data;
using jett_exchange_backend.Messaging;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Services.Notifications;

public class TicketAvailableNotifier(AppDbContext dbContext, IEmailMessagePublisher emailPublisher, ILogger<TicketAvailableNotifier> logger)
    : ITicketAvailableNotifier
{
    public async Task NotifySubscribersAsync(TicketAvailableMessage message, CancellationToken cancellationToken = default)
    {
        var subscriptions = await dbContext.TicketDateSubscriptions
            .Where(s => s.Date == message.Date && !s.Notified)
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            try
            {
                await emailPublisher.PublishAsync(new SendEmailMessage
                {
                    To = subscription.Email,
                    Subject = "A ticket is available for your requested date",
                    Body = $"A ticket is now available for sale on {message.Date:yyyy-MM-dd}. " +
                           "Check the Jett Ticket Exchange to grab it before it's gone."
                }, cancellationToken);

                subscription.Notified = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish email notification for {Email} for date {Date}",
                    subscription.Email, message.Date);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
