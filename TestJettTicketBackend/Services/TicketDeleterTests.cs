using jett_exchange_backend.Data;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.Tickets;
using FluentAssertions;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Services;

public class TicketDeleterTests
{
    private string _databaseName = null!;
    private AppDbContext _dbContext = null!;
    private TicketDeleteTokenService _deleteTokenService = null!;
    private TicketDeleter _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = Guid.NewGuid().ToString();
        _dbContext = InMemoryDbContextFactory.Create(_databaseName);
        _deleteTokenService = TestTicketDeleteTokenService.Create();
        _sut = new TicketDeleter(_dbContext, _deleteTokenService);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private Ticket? GetPersistedTicket(Guid id)
    {
        using var readContext = InMemoryDbContextFactory.Create(_databaseName);
        return readContext.Tickets.FirstOrDefault(t => t.Id == id);
    }

    private static Ticket CreateTicket(string pin = "PIN123") => new()
    {
        TicketId = "TCK-1",
        OriginalOwnerName = "Owner",
        OriginalOwnerPassportNumber = "P1",
        NumberOfBags = 1,
        TotalPrice = 10m,
        SellerName = "Seller",
        SellerEmail = "seller@example.com",
        SellerPhone = "+1234567890",
        PaymentMethod = PaymentMethod.Reflect,
        PaymentInfo = new Reflect { PhoneNumber = "0791234567" },
        Pin = pin,
        Status = TicketSellStatus.ForSale,
        TicketFilePath = "path.pdf"
    };

    [Test]
    public async Task DeleteByIdAsync_MarksTicketDeleted_WhenFound()
    {
        var ticket = CreateTicket();
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.DeleteByIdAsync(ticket.Id);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        GetPersistedTicket(ticket.Id)!.Status.Should().Be(TicketSellStatus.Deleted);
    }

    [Test]
    public async Task DeleteByIdAsync_ReturnsNotFound_WhenMissing()
    {
        var result = await _sut.DeleteByIdAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task DeleteByTokenAsync_MarksTicketDeleted_WhenTokenValid()
    {
        var ticket = CreateTicket(pin: "XYZ789");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);

        var result = await _sut.DeleteByTokenAsync(token);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        GetPersistedTicket(ticket.Id)!.Status.Should().Be(TicketSellStatus.Deleted);
    }

    [Test]
    public async Task DeleteByTokenAsync_ReturnsNotFound_WhenTicketNoLongerExists()
    {
        var token = _deleteTokenService.GenerateToken(Guid.NewGuid());

        var result = await _sut.DeleteByTokenAsync(token);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task DeleteByTokenAsync_ReturnsUnauthorized_WhenTokenIsMalformed()
    {
        var result = await _sut.DeleteByTokenAsync("not-a-real-token");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task DeleteByTokenAsync_ReturnsUnauthorized_WhenTokenSignedWithDifferentKey()
    {
        var otherService = TestTicketDeleteTokenService.Create();
        var ticket = CreateTicket(pin: "SIGNED-ELSEWHERE");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        // A token forged with an unrelated signing key must not be accepted.
        var forgedToken = otherService.GenerateToken(ticket.Id);
        var tamperedSut = new TicketDeleter(_dbContext, TestTicketDeleteTokenService.Create());

        var result = await tamperedSut.DeleteByTokenAsync(forgedToken.Replace(forgedToken.Split('.')[2], "tampered-signature"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task DeleteByTokenAsync_ReturnsUnauthorized_WhenTokenExpired()
    {
        var expiredTokenService = TestTicketDeleteTokenService.Create(expirationMinutes: -1);
        var ticket = CreateTicket(pin: "EXPIRED");
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = expiredTokenService.GenerateToken(ticket.Id);

        var result = await _sut.DeleteByTokenAsync(token);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }
}
