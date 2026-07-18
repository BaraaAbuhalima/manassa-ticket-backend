using manassa_ticket_backend.Data;
using manassa_ticket_backend.Models;
using manassa_ticket_backend.Services.Payments;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestManassaTicketBackend.TestHelpers;

namespace TestManassaTicketBackend.Services;

public class TicketReservationSweeperTests
{
    private string _databaseName = null!;
    private AppDbContext _dbContext = null!;
    private ServiceProvider _serviceProvider = null!;
    private TicketReservationSweeper _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = Guid.NewGuid().ToString();
        _dbContext = InMemoryDbContextFactory.Create(_databaseName);

        var services = new ServiceCollection();
        services.AddScoped(_ => InMemoryDbContextFactory.Create(_databaseName));
        _serviceProvider = services.BuildServiceProvider();

        _sut = new TicketReservationSweeper(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TicketReservationSweeper>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _sut.Dispose();
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    private Ticket GetPersistedTicket(Guid id)
    {
        using var readContext = InMemoryDbContextFactory.Create(_databaseName);
        return readContext.Tickets.Single(t => t.Id == id);
    }

    private static Ticket CreateReservedTicket(string pin, DateTime reservedAt) => new()
    {
        TicketId = "TCK-1",
        SellerName = "Seller",
        SellerEmail = "seller@example.com",
        SellerPhone = "+1234567890",
        TotalPriceJod = 50m,
        TotalPriceUsd = 71.5m,
        PaymentMethod = PaymentMethod.Reflect,
        PaymentInfo = new Reflect { PhoneNumber = "0791234567" },
        Pin = pin,
        Status = TicketSellStatus.Reserved,
        ReservedAt = reservedAt,
        BuyerName = "Buyer",
        BuyerEmail = "buyer@example.com",
        StripePaymentIntentId = "pi_123",
        TicketFilePath = "permanent/file.pdf"
    };

    [Test]
    public async Task SweepOnceAsync_ReleasesExpiredReservation_BackToForSale()
    {
        var ticket = CreateReservedTicket("EXPIRED", DateTime.UtcNow - TicketPurchaseService.ReservationTtl - TimeSpan.FromMinutes(1));
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var released = await _sut.SweepOnceAsync(CancellationToken.None);

        released.Should().Be(1);
        var saved = GetPersistedTicket(ticket.Id);
        saved.Status.Should().Be(TicketSellStatus.ForSale);
        saved.ReservedAt.Should().BeNull();
        saved.BuyerName.Should().BeNull();
        saved.BuyerEmail.Should().BeNull();
        saved.StripePaymentIntentId.Should().BeNull();
    }

    [Test]
    public async Task SweepOnceAsync_LeavesActiveReservation_Untouched()
    {
        var ticket = CreateReservedTicket("ACTIVE", DateTime.UtcNow);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var released = await _sut.SweepOnceAsync(CancellationToken.None);

        released.Should().Be(0);
        var saved = GetPersistedTicket(ticket.Id);
        saved.Status.Should().Be(TicketSellStatus.Reserved);
    }
}