using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.Models;
using manassa_ticket_backend.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TestManassaTicketBackend.TestHelpers;

namespace TestManassaTicketBackend.Services;

public class TicketReaderTests
{
    private AppDbContext _dbContext = null!;
    private TicketReader _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _dbContext = InMemoryDbContextFactory.Create();
        var feeOptions = Options.Create(new FeeOptions { FlatFeeUsd = 1m, PercentFee = 5m });
        _sut = new TicketReader(_dbContext, feeOptions);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private static Ticket CreateTicket(
        string pin = "PIN123",
        DateTime? ticketDateTime = null,
        TicketSellStatus status = TicketSellStatus.ForSale) => new()
        {
            TicketId = Guid.NewGuid().ToString("N")[..10],
            OriginalOwnerName = "Owner",
            OriginalOwnerPassportNumber = "P1",
            TicketDateTime = ticketDateTime ?? DateTime.UtcNow,
            NumberOfBags = 1,
            SellerAskedPriceJod = 10m,
            OriginalPrice = 14.3m,
            SellerName = "Seller",
            SellerEmail = "seller@example.com",
            SellerPhone = "+1234567890",
            PaymentMethod = PaymentMethod.Reflect,
            PaymentInfo = new Reflect { PhoneNumber = "0791234567" },
            Pin = pin,
            Status = status,
            TicketFilePath = "path.pdf"
        };

    [Test]
    public async Task GetByIdAsync_ReturnsTicket_WhenFound()
    {
        var ticket = CreateTicket();
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(ticket.Id);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(ticket.Id);
        result.Links.Should().ContainKey("self");
    }

    [Test]
    public async Task GetByIdAsync_IncludesFeeInclusiveAmount()
    {
        // SellerAskedPriceJod 10 JOD -> baseUsd 14.3 -> fee = 1 + 5% of 14.3 = 1.715 -> rounds to 1.72 -> totalUsd 16.02
        // totalUsd 16.02 * UsdToJodRate 0.68 = 10.8936 -> rounds to 10.89
        var ticket = CreateTicket();
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(ticket.Id);

        result.Data!.TotalPriceUsd.Should().Be(16.02m);
        result.Data!.TotalPriceJod.Should().Be(10.89m);
    }

    [Test]
    public async Task GetByIdAsync_ReturnsNotFound_WhenMissing()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Data.Should().BeNull();
    }

    [Test]
    public async Task GetByPinAsync_ReturnsTicket_WhenFound()
    {
        var ticket = CreateTicket(pin: "ABC123");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByPinAsync("ABC123", "seller@example.com");

        result.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(ticket.Id);
        result.Data!.Status.Should().Be(ticket.Status);
    }

    [Test]
    public async Task GetByPinAsync_ReturnsTicket_WhenEmailDiffersOnlyByCase()
    {
        var ticket = CreateTicket(pin: "ABC123");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByPinAsync("ABC123", "SELLER@EXAMPLE.COM");

        result.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(ticket.Id);
    }

    [Test]
    public async Task GetByPinAsync_ReturnsNotFound_WhenMissing()
    {
        var result = await _sut.GetByPinAsync("does-not-exist", "seller@example.com");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task GetByPinAsync_ReturnsNotFound_WhenEmailDoesNotMatchPin()
    {
        var ticket = CreateTicket(pin: "ABC123");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByPinAsync("ABC123", "someone-else@example.com");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task GetByPinAsync_IncludesDeleteLink()
    {
        var ticket = CreateTicket(pin: "ABC123");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByPinAsync("ABC123", "seller@example.com");

        result.Links.Should().ContainKey("delete");
        result.Links!["delete"].Should().Be("/api/ticket");
    }

    [Test]
    public async Task GetByPinAsync_IncludesRepublishLink()
    {
        var ticket = CreateTicket(pin: "ABC123");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByPinAsync("ABC123", "seller@example.com");

        result.Links.Should().ContainKey("republish");
        result.Links!["republish"].Should().Be("/api/ticket/republish");
    }

    [Test]
    public async Task GetByPinAsync_IncludesModifyLink()
    {
        var ticket = CreateTicket(pin: "ABC123");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByPinAsync("ABC123", "seller@example.com");

        result.Links.Should().ContainKey("modify");
        result.Links!["modify"].Should().Be("/api/ticket");
    }

    [Test]
    public async Task GetByPinAsync_IncludesSellerAndPaymentDetails()
    {
        var ticket = CreateTicket(pin: "ABC123");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByPinAsync("ABC123", "seller@example.com");

        result.Data!.SellerEmail.Should().Be(ticket.SellerEmail);
        result.Data!.SellerPhone.Should().Be(ticket.SellerPhone);
        result.Data!.PaymentMethod.Should().Be(ticket.PaymentMethod);
        result.Data!.SellerAskedPriceJod.Should().Be(ticket.SellerAskedPriceJod);
        result.Data!.PaymentInfo.Should().BeOfType<Reflect>();
        ((Reflect)result.Data!.PaymentInfo).PhoneNumber.Should().Be("0791234567");
    }

    [Test]
    public async Task GetByPinAsync_IncludesSoldAt_WhenTicketSold()
    {
        var ticket = CreateTicket(pin: "SOLD-1", status: TicketSellStatus.Sold);
        ticket.SoldAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByPinAsync("SOLD-1", "seller@example.com");

        result.Data!.SoldAt.Should().Be(ticket.SoldAt);
    }

    [Test]
    public async Task GetByPinAsync_SurfacesRejectionReasonAsMessage_WhenTicketRejected()
    {
        var ticket = CreateTicket(pin: "REJ-1");
        ticket.RejectionReason = "Ticket file failed verification";
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByPinAsync("REJ-1", "seller@example.com");

        result.Message.Should().Be("Ticket file failed verification");
    }

    [Test]
    public async Task GetForDateAsync_ReturnsOnlyTicketsForThatDate()
    {
        var date = new DateOnly(2026, 7, 5);
        var onDate = CreateTicket(pin: "ONDATE", ticketDateTime: date.ToDateTime(new TimeOnly(10, 0)));
        var dayBefore = CreateTicket(pin: "BEFORE", ticketDateTime: date.AddDays(-1).ToDateTime(TimeOnly.MinValue));
        var dayAfter = CreateTicket(pin: "AFTER", ticketDateTime: date.AddDays(1).ToDateTime(TimeOnly.MinValue));
        _dbContext.Tickets.AddRange(onDate, dayBefore, dayAfter);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetForDateAsync(date, page: 1);

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle(t => t.Id == onDate.Id);
        result.Meta!.TotalCount.Should().Be(1);
    }

    [Test]
    public async Task GetForDateAsync_ExcludesTicketsNotForSale()
    {
        var date = new DateOnly(2026, 7, 5);
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var forSale = CreateTicket(pin: "FORSALE", ticketDateTime: dateTime, status: TicketSellStatus.ForSale);
        var sold = CreateTicket(pin: "SOLD", ticketDateTime: dateTime, status: TicketSellStatus.Sold);
        var deleted = CreateTicket(pin: "DELETED", ticketDateTime: dateTime, status: TicketSellStatus.Deleted);
        _dbContext.Tickets.AddRange(forSale, sold, deleted);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetForDateAsync(date, page: 1);

        result.Data.Should().ContainSingle(t => t.Id == forSale.Id);
    }

    [Test]
    public async Task GetForDateAsync_PaginatesResults_TwentyPerPage()
    {
        var date = new DateOnly(2026, 7, 5);
        var tickets = Enumerable.Range(0, 25)
            .Select(i => CreateTicket(pin: $"PIN{i:D3}", ticketDateTime: date.ToDateTime(TimeOnly.MinValue).AddMinutes(i)))
            .ToList();
        _dbContext.Tickets.AddRange(tickets);
        await _dbContext.SaveChangesAsync();

        var page1 = await _sut.GetForDateAsync(date, page: 1);
        var page2 = await _sut.GetForDateAsync(date, page: 2);

        page1.Data.Should().HaveCount(20);
        page1.Meta!.Page.Should().Be(1);
        page1.Meta.PageSize.Should().Be(20);
        page1.Meta.TotalCount.Should().Be(25);

        page2.Data.Should().HaveCount(5);
        page2.Meta!.Page.Should().Be(2);

        page1.Data!.Select(t => t.Id).Should().NotIntersectWith(page2.Data!.Select(t => t.Id));
    }

    [Test]
    public async Task GetForDateAsync_ClampsPageBelowOne_ToPageOne()
    {
        var date = new DateOnly(2026, 7, 5);
        _dbContext.Tickets.Add(CreateTicket(ticketDateTime: date.ToDateTime(TimeOnly.MinValue)));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetForDateAsync(date, page: 0);

        result.Meta!.Page.Should().Be(1);
        result.Data.Should().HaveCount(1);
    }

    [Test]
    public async Task GetForDateRangeAsync_ReturnsTicketsWithinInclusiveRange()
    {
        var start = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2026, 7, 3);

        var before = CreateTicket(pin: "BEFORE", ticketDateTime: new DateTime(2026, 6, 30, 12, 0, 0));
        var onStart = CreateTicket(pin: "ON_START", ticketDateTime: new DateTime(2026, 7, 1, 0, 0, 0));
        var inside = CreateTicket(pin: "INSIDE", ticketDateTime: new DateTime(2026, 7, 2, 15, 0, 0));
        var onEnd = CreateTicket(pin: "ON_END", ticketDateTime: new DateTime(2026, 7, 3, 23, 59, 0));
        var after = CreateTicket(pin: "AFTER", ticketDateTime: new DateTime(2026, 7, 4, 0, 0, 0));
        _dbContext.Tickets.AddRange(before, onStart, inside, onEnd, after);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetForDateRangeAsync(start, end, page: 1);

        result.Success.Should().BeTrue();
        result.Data!.Select(t => t.Id).Should().BeEquivalentTo([onStart.Id, inside.Id, onEnd.Id]);
    }

    [Test]
    public async Task GetForDateRangeAsync_ReturnsBadRequest_WhenStartDateAfterEndDate()
    {
        var result = await _sut.GetForDateRangeAsync(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 1), page: 1);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task GetForDateRangeAsync_PaginatesResults_TwentyPerPage()
    {
        var start = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2026, 7, 10);
        var tickets = Enumerable.Range(0, 22)
            .Select(i => CreateTicket(pin: $"PIN{i:D3}", ticketDateTime: new DateTime(2026, 7, 1).AddHours(i)))
            .ToList();
        _dbContext.Tickets.AddRange(tickets);
        await _dbContext.SaveChangesAsync();

        var page1 = await _sut.GetForDateRangeAsync(start, end, page: 1);
        var page2 = await _sut.GetForDateRangeAsync(start, end, page: 2);

        page1.Data.Should().HaveCount(20);
        page2.Data.Should().HaveCount(2);
        page2.Meta!.TotalCount.Should().Be(22);
    }
}
