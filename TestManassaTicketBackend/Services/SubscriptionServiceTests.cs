using manassa_ticket_backend.Data;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Models;
using manassa_ticket_backend.Services.Subscriptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TestManassaTicketBackend.TestHelpers;

namespace TestManassaTicketBackend.Services;

public class SubscriptionServiceTests
{
    private AppDbContext _dbContext = null!;
    private SubscriptionService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _dbContext = InMemoryDbContextFactory.Create();
        _sut = new SubscriptionService(_dbContext);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    [Test]
    public async Task SubscribeAsync_CreatesNewSubscription_WhenNoneExists()
    {
        var request = new SubscribeRequest { Email = "user@example.com", Date = new DateOnly(2026, 8, 15) };

        var result = await _sut.SubscribeAsync(request);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        var subscription = await _dbContext.TicketDateSubscriptions.SingleAsync();
        subscription.Email.Should().Be("user@example.com");
        subscription.Date.Should().Be(new DateOnly(2026, 8, 15));
        subscription.Notified.Should().BeFalse();
    }

    [Test]
    public async Task SubscribeAsync_DoesNotCreateDuplicate_WhenAlreadySubscribed()
    {
        var request = new SubscribeRequest { Email = "user@example.com", Date = new DateOnly(2026, 8, 15) };
        await _sut.SubscribeAsync(request);

        await _sut.SubscribeAsync(request);

        (await _dbContext.TicketDateSubscriptions.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task SubscribeAsync_ResetsNotifiedFlag_WhenResubscribingAfterNotification()
    {
        var subscription = new TicketDateSubscription
        {
            Email = "user@example.com",
            Date = new DateOnly(2026, 8, 15),
            Notified = true
        };
        _dbContext.TicketDateSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        await _sut.SubscribeAsync(new SubscribeRequest { Email = "user@example.com", Date = new DateOnly(2026, 8, 15) });

        var updated = await _dbContext.TicketDateSubscriptions.SingleAsync();
        updated.Notified.Should().BeFalse();
    }

    [Test]
    public async Task SubscribeAsync_CreatesSeparateSubscriptions_ForDifferentDates()
    {
        await _sut.SubscribeAsync(new SubscribeRequest { Email = "user@example.com", Date = new DateOnly(2026, 8, 15) });
        await _sut.SubscribeAsync(new SubscribeRequest { Email = "user@example.com", Date = new DateOnly(2026, 8, 16) });

        (await _dbContext.TicketDateSubscriptions.CountAsync()).Should().Be(2);
    }
}
