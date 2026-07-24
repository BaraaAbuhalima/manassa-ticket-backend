using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.Messaging;
using manassa_ticket_backend.Models;
using manassa_ticket_backend.Services.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TestManassaTicketBackend.TestHelpers;

namespace TestManassaTicketBackend.Services;

public class TicketAvailableNotifierTests
{
    private AppDbContext _dbContext = null!;
    private Mock<IEmailMessagePublisher> _emailPublisher = null!;
    private TicketAvailableNotifier _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _dbContext = InMemoryDbContextFactory.Create();
        _emailPublisher = new Mock<IEmailMessagePublisher>();
        var frontendOptions = Options.Create(new FrontendOptions { BaseUrl = "https://manassa-ticket.test" });
        _sut = new TicketAvailableNotifier(_dbContext, _emailPublisher.Object, frontendOptions, NullLogger<TicketAvailableNotifier>.Instance);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private static TicketDateSubscription CreateSubscription(string email, DateOnly date, bool notified = false) => new()
    {
        Email = email,
        Date = date,
        Notified = notified
    };

    [Test]
    public async Task NotifySubscribersAsync_PublishesEmailMessage_ToMatchingUnnotifiedSubscribers()
    {
        var date = new DateOnly(2026, 8, 15);
        _dbContext.TicketDateSubscriptions.Add(CreateSubscription("a@example.com", date));
        _dbContext.TicketDateSubscriptions.Add(CreateSubscription("b@example.com", date));
        await _dbContext.SaveChangesAsync();

        await _sut.NotifySubscribersAsync(new TicketAvailableMessage { TicketId = Guid.NewGuid(), Date = date });

        _emailPublisher.Verify(e => e.PublishAsync(
            It.Is<SendEmailMessage>(m => m.To == "a@example.com"), It.IsAny<CancellationToken>()), Times.Once);
        _emailPublisher.Verify(e => e.PublishAsync(
            It.Is<SendEmailMessage>(m => m.To == "b@example.com"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task NotifySubscribersAsync_MarksSubscriptionsNotified_AfterPublishing()
    {
        var date = new DateOnly(2026, 8, 15);
        var subscription = CreateSubscription("a@example.com", date);
        _dbContext.TicketDateSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        await _sut.NotifySubscribersAsync(new TicketAvailableMessage { TicketId = Guid.NewGuid(), Date = date });

        subscription.Notified.Should().BeTrue();
    }

    [Test]
    public async Task NotifySubscribersAsync_IgnoresSubscriptionsForDifferentDates()
    {
        _dbContext.TicketDateSubscriptions.Add(CreateSubscription("a@example.com", new DateOnly(2026, 8, 16)));
        await _dbContext.SaveChangesAsync();

        await _sut.NotifySubscribersAsync(new TicketAvailableMessage { TicketId = Guid.NewGuid(), Date = new DateOnly(2026, 8, 15) });

        _emailPublisher.Verify(e => e.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task NotifySubscribersAsync_SkipsAlreadyNotifiedSubscriptions()
    {
        var date = new DateOnly(2026, 8, 15);
        _dbContext.TicketDateSubscriptions.Add(CreateSubscription("a@example.com", date, notified: true));
        await _dbContext.SaveChangesAsync();

        await _sut.NotifySubscribersAsync(new TicketAvailableMessage { TicketId = Guid.NewGuid(), Date = date });

        _emailPublisher.Verify(e => e.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task NotifySubscribersAsync_ContinuesNotifyingOthers_WhenOnePublishFails()
    {
        var date = new DateOnly(2026, 8, 15);
        var failing = CreateSubscription("fails@example.com", date);
        var succeeding = CreateSubscription("ok@example.com", date);
        _dbContext.TicketDateSubscriptions.AddRange(failing, succeeding);
        await _dbContext.SaveChangesAsync();

        _emailPublisher.Setup(e => e.PublishAsync(
                It.Is<SendEmailMessage>(m => m.To == "fails@example.com"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        await _sut.NotifySubscribersAsync(new TicketAvailableMessage { TicketId = Guid.NewGuid(), Date = date });

        _emailPublisher.Verify(e => e.PublishAsync(
            It.Is<SendEmailMessage>(m => m.To == "ok@example.com"), It.IsAny<CancellationToken>()), Times.Once);
        succeeding.Notified.Should().BeTrue();
        failing.Notified.Should().BeFalse();
    }

    [Test]
    public async Task NotifySubscribersAsync_IncludesDateInEmailBody()
    {
        var date = new DateOnly(2026, 8, 15);
        _dbContext.TicketDateSubscriptions.Add(CreateSubscription("a@example.com", date));
        await _dbContext.SaveChangesAsync();

        await _sut.NotifySubscribersAsync(new TicketAvailableMessage { TicketId = Guid.NewGuid(), Date = date });

        _emailPublisher.Verify(e => e.PublishAsync(
            It.Is<SendEmailMessage>(m => m.Body.Contains("2026-08-15")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task NotifySubscribersAsync_IncludesFrontendTicketLinkInEmailBody()
    {
        var date = new DateOnly(2026, 8, 15);
        _dbContext.TicketDateSubscriptions.Add(CreateSubscription("a@example.com", date));
        await _dbContext.SaveChangesAsync();

        await _sut.NotifySubscribersAsync(new TicketAvailableMessage { TicketId = Guid.NewGuid(), Date = date });

        _emailPublisher.Verify(e => e.PublishAsync(
            It.Is<SendEmailMessage>(m => m.Body.Contains("https://manassa-ticket.test/tickets?date=2026-08-15")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
