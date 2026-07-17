using jett_exchange_backend.Data;
using jett_exchange_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Services.Payments;

// Releases purchase holds a buyer abandoned mid-checkout. Without this, a Reserved
// ticket whose buyer never completes payment would stay hidden from listings forever,
// since nothing else ever flips it back to ForSale.
public class TicketReservationSweeper(IServiceScopeFactory scopeFactory, ILogger<TicketReservationSweeper> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to release expired ticket reservations");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow - TicketPurchaseService.ReservationTtl;
        var released = await dbContext.Tickets
            .Where(t => t.Status == TicketSellStatus.Reserved && t.ReservedAt < cutoff)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Status, TicketSellStatus.ForSale)
                .SetProperty(t => t.ReservedAt, (DateTime?)null)
                .SetProperty(t => t.BuyerName, (string?)null)
                .SetProperty(t => t.BuyerEmail, (string?)null)
                .SetProperty(t => t.StripePaymentIntentId, (string?)null),
                cancellationToken);

        if (released > 0)
        {
            logger.LogInformation("Released {Count} expired ticket reservations back to ForSale", released);
        }

        return released;
    }
}