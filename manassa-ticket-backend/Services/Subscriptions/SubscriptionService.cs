using manassa_ticket_backend.Common;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace manassa_ticket_backend.Services.Subscriptions;

public class SubscriptionService(AppDbContext dbContext) : ISubscriptionService
{
    public async Task<ApiResponse<string>> SubscribeAsync(SubscribeRequest request)
    {
        var existing = await dbContext.TicketDateSubscriptions
            .FirstOrDefaultAsync(s => s.Email == request.Email && s.Date == request.Date);

        if (existing is not null)
        {
            existing.Notified = false;
        }
        else
        {
            dbContext.TicketDateSubscriptions.Add(new TicketDateSubscription
            {
                Email = request.Email,
                Date = request.Date
            });
        }

        await dbContext.SaveChangesAsync();

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Subscribed successfully. You'll receive an email when a ticket becomes available for this date."
        };
    }
}
