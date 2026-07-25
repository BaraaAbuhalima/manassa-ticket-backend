using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.Notifications;

public class TicketAvailableNotifier(
    AppDbContext dbContext,
    IEmailMessagePublisher emailPublisher,
    IOptions<FrontendOptions> frontendOptions,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<TicketAvailableNotifier> logger)
    : ITicketAvailableNotifier
{
    public async Task NotifySubscribersAsync(TicketAvailableMessage message, CancellationToken cancellationToken = default)
    {
        var subscriptions = await dbContext.TicketDateSubscriptions
            .Where(s => s.Date == message.Date && !s.Notified)
            .ToListAsync(cancellationToken);

        var ticketLink = $"{frontendOptions.Value.BaseUrl.TrimEnd('/')}/tickets?date={message.Date:yyyy-MM-dd}";

        foreach (var subscription in subscriptions)
        {
            try
            {
                await emailPublisher.PublishAsync(new SendEmailMessage
                {
                    To = subscription.Email,
                    From = smtpOptions.Value.FromAddress,
                    Subject = "A ticket is available for your requested date | تتوفر تذكرة بالتاريخ الذي طلبته",
                    Body = $"""
                        A ticket is now available for sale on {message.Date:yyyy-MM-dd}. Check Manassa Ticket Exchange to grab it before it's gone.

                        {ticketLink}

                        ---

                        تتوفر الآن تذكرة للبيع بتاريخ {message.Date:yyyy-MM-dd}. تفقّد منصة Manassa Ticket Exchange للحصول عليها قبل نفادها.

                        {ticketLink}
                        """
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
