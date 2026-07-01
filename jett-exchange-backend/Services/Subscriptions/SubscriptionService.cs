using jett_exchange_backend.Common;
using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Services.Subscriptions;

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
