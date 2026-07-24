using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Messaging;
using manassa_ticket_backend.Models;
using manassa_ticket_backend.Services.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TestManassaTicketBackend.TestHelpers;

namespace TestManassaTicketBackend.Services;

// CreatePaymentIntentAsync's success path calls the live Stripe SDK directly (there's no
// mockable seam for it), so these tests only cover the advisory availability check that runs
// before any Stripe call is made. The actual claim now happens later, in the webhook handler's
// ClaimAndCaptureAsync once Stripe confirms a card is good — not covered here for the same
// reason.
public class TicketPurchaseServiceTests
{
    private string _databaseName = null!;
    private AppDbContext _dbContext = null!;
    private TicketPurchaseService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = Guid.NewGuid().ToString();
        _dbContext = InMemoryDbContextFactory.Create(_databaseName);

        var options = Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_fake",
            PublishableKey = "pk_test_fake",
            WebhookSecret = "whsec_fake",
            Currency = "usd"
        });
        var feeOptions = Options.Create(new FeeOptions
        {
            FlatFeeUsd = 1m,
            PercentFee = 5m
        });

        _sut = new TicketPurchaseService(
            _dbContext,
            options,
            feeOptions,
            Mock.Of<ITicketSoldNotificationPublisher>(),
            Mock.Of<ITicketPurchasedNotificationPublisher>(),
            NullLogger<TicketPurchaseService>.Instance);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private Ticket? GetPersistedTicket(Guid id)
    {
        using var readContext = InMemoryDbContextFactory.Create(_databaseName);
        return readContext.Tickets.FirstOrDefault(t => t.Id == id);
    }

    private static Ticket CreateTicket(string pin, TicketSellStatus status, DateTime? reservedAt = null) => new()
    {
        TicketId = "TCK-1",
        SellerName = "Seller",
        SellerEmail = "seller@example.com",
        SellerPhone = "+1234567890",
        SellerAskedPriceJod = 50m,
        PaymentMethod = PaymentMethod.Reflect,
        PaymentInfo = new Reflect { PhoneNumber = "0791234567" },
        Pin = pin,
        Status = status,
        ReservedAt = reservedAt,
        TicketFilePath = "permanent/file.pdf"
    };

    private static PurchaseTicketRequest CreateRequest() => new()
    {
        BuyerName = "Buyer",
        BuyerEmail = "buyer@example.com"
    };

    [Test]
    public async Task CreatePaymentIntentAsync_ReturnsNotFound_WhenTicketDoesNotExist()
    {
        var result = await _sut.CreatePaymentIntentAsync(Guid.NewGuid(), CreateRequest());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task CreatePaymentIntentAsync_ReturnsConflict_WhenTicketAlreadySold()
    {
        var ticket = CreateTicket("SOLD", TicketSellStatus.Sold);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.CreatePaymentIntentAsync(ticket.Id, CreateRequest());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        GetPersistedTicket(ticket.Id)!.Status.Should().Be(TicketSellStatus.Sold);
    }

    [Test]
    public async Task CreatePaymentIntentAsync_ReturnsConflict_WhenReservationStillActive()
    {
        var ticket = CreateTicket("RESERVED", TicketSellStatus.Reserved, reservedAt: DateTime.UtcNow);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.CreatePaymentIntentAsync(ticket.Id, CreateRequest());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        var saved = GetPersistedTicket(ticket.Id)!;
        saved.Status.Should().Be(TicketSellStatus.Reserved);
        saved.BuyerEmail.Should().BeNull();
    }

    // The success path (expired-reservation reclaim, or a fresh ForSale purchase) proceeds
    // to call the live Stripe API with no mockable seam, so it isn't covered here — same gap
    // as the rest of this service already had before this change.
}
