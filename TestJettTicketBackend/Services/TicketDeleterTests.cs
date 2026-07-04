using jett_exchange_backend.Common.ValueObjects;
using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Messaging;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Services;

public class TicketDeleterTests
{
    private string _databaseName = null!;
    private AppDbContext _dbContext = null!;
    private TicketDeleteTokenService _deleteTokenService = null!;
    private Mock<ITicketAvailablePublisher> _availablePublisher = null!;
    private TicketDeleter _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = Guid.NewGuid().ToString();
        _dbContext = InMemoryDbContextFactory.Create(_databaseName);
        _deleteTokenService = TestTicketDeleteTokenService.Create();
        _availablePublisher = new Mock<ITicketAvailablePublisher>();
        _sut = new TicketDeleter(_dbContext, _deleteTokenService, _availablePublisher.Object, NullLogger<TicketDeleter>.Instance);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private Ticket? GetPersistedTicket(Guid id)
    {
        using var readContext = InMemoryDbContextFactory.Create(_databaseName);
        return readContext.Tickets.FirstOrDefault(t => t.Id == id);
    }

    private static Ticket CreateTicket(string pin = "PIN123", TicketSellStatus status = TicketSellStatus.ForSale, decimal originalPrice = 10m) => new()
    {
        TicketId = "TCK-1",
        OriginalOwnerName = "Owner",
        OriginalOwnerPassportNumber = "P1",
        NumberOfBags = 1,
        TotalPrice = 10m,
        OriginalPrice = originalPrice,
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
    public async Task DeleteByIdAsync_ReturnsConflict_WhenTicketAlreadySold()
    {
        var ticket = CreateTicket(status: TicketSellStatus.Sold);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.DeleteByIdAsync(ticket.Id);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        GetPersistedTicket(ticket.Id)!.Status.Should().Be(TicketSellStatus.Sold);
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
    public async Task DeleteByTokenAsync_ReturnsConflict_WhenTicketAlreadySold()
    {
        var ticket = CreateTicket(pin: "SOLD-PIN", status: TicketSellStatus.Sold);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);

        var result = await _sut.DeleteByTokenAsync(token);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        GetPersistedTicket(ticket.Id)!.Status.Should().Be(TicketSellStatus.Sold);
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
        var tamperedSut = new TicketDeleter(_dbContext, TestTicketDeleteTokenService.Create(), _availablePublisher.Object, NullLogger<TicketDeleter>.Instance);

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

    [Test]
    public async Task RepublishByTokenAsync_MarksTicketForSale_WhenTicketWasDeleted()
    {
        var ticket = CreateTicket(pin: "REPUBLISH-1", status: TicketSellStatus.Deleted);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);

        var result = await _sut.RepublishByTokenAsync(token);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        GetPersistedTicket(ticket.Id)!.Status.Should().Be(TicketSellStatus.ForSale);
        _availablePublisher.Verify(p => p.PublishAsync(It.Is<TicketAvailableMessage>(m => m.TicketId == ticket.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RepublishByTokenAsync_ReturnsConflict_WhenTicketIsForSale()
    {
        var ticket = CreateTicket(pin: "REPUBLISH-2", status: TicketSellStatus.ForSale);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);

        var result = await _sut.RepublishByTokenAsync(token);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        GetPersistedTicket(ticket.Id)!.Status.Should().Be(TicketSellStatus.ForSale);
    }

    [Test]
    public async Task RepublishByTokenAsync_ReturnsConflict_WhenTicketAlreadySold()
    {
        var ticket = CreateTicket(pin: "REPUBLISH-3", status: TicketSellStatus.Sold);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);

        var result = await _sut.RepublishByTokenAsync(token);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        GetPersistedTicket(ticket.Id)!.Status.Should().Be(TicketSellStatus.Sold);
    }

    [Test]
    public async Task RepublishByTokenAsync_ReturnsNotFound_WhenTicketNoLongerExists()
    {
        var token = _deleteTokenService.GenerateToken(Guid.NewGuid());

        var result = await _sut.RepublishByTokenAsync(token);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task RepublishByTokenAsync_ReturnsUnauthorized_WhenTokenIsMalformed()
    {
        var result = await _sut.RepublishByTokenAsync("not-a-real-token");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task ModifyTicketByTokenAsync_UpdatesPaymentMethodAndInfo_WhenTicketForSale()
    {
        var ticket = CreateTicket(pin: "PAY-1", status: TicketSellStatus.ForSale);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);
        var request = new UpdateTicketRequest
        {
            Payment = new PaymentUpdateRequest
            {
                PaymentMethod = PaymentMethod.Iban,
                PaymentInfoRequest = new PaymentInfoRequest
                {
                    BankDetails = new BankDetails
                    {
                        AccountNumber = "1",
                        BankName = "B",
                        Country = "JO",
                        AccountHolderName = "A"
                    }
                }
            }
        };

        var result = await _sut.ModifyTicketByTokenAsync(token, request);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        var persisted = GetPersistedTicket(ticket.Id)!;
        persisted.PaymentMethod.Should().Be(PaymentMethod.Iban);
        persisted.PaymentInfo.Should().BeOfType<BankTransferInfo>();
        ((BankTransferInfo)persisted.PaymentInfo).BankDetails.AccountNumber.Should().Be("1");
    }

    [Test]
    public async Task ModifyTicketByTokenAsync_ReturnsConflict_WhenTicketAlreadySold()
    {
        var ticket = CreateTicket(pin: "PAY-2", status: TicketSellStatus.Sold);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);
        var request = new UpdateTicketRequest
        {
            Payment = new PaymentUpdateRequest
            {
                PaymentMethod = PaymentMethod.Phone,
                PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0797654321" }
            }
        };

        var result = await _sut.ModifyTicketByTokenAsync(token, request);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        GetPersistedTicket(ticket.Id)!.PaymentMethod.Should().Be(PaymentMethod.Reflect);
    }

    [Test]
    public async Task ModifyTicketByTokenAsync_ReturnsConflict_WhenTicketDeleted()
    {
        var ticket = CreateTicket(pin: "PAY-3", status: TicketSellStatus.Deleted);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);
        var request = new UpdateTicketRequest
        {
            Payment = new PaymentUpdateRequest
            {
                PaymentMethod = PaymentMethod.Reflect,
                PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0797654321" }
            }
        };

        var result = await _sut.ModifyTicketByTokenAsync(token, request);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Test]
    public async Task ModifyTicketByTokenAsync_ReturnsNotFound_WhenTicketNoLongerExists()
    {
        var token = _deleteTokenService.GenerateToken(Guid.NewGuid());
        var request = new UpdateTicketRequest
        {
            Payment = new PaymentUpdateRequest
            {
                PaymentMethod = PaymentMethod.Reflect,
                PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0797654321" }
            }
        };

        var result = await _sut.ModifyTicketByTokenAsync(token, request);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task ModifyTicketByTokenAsync_ReturnsUnauthorized_WhenTokenIsMalformed()
    {
        var request = new UpdateTicketRequest
        {
            Payment = new PaymentUpdateRequest
            {
                PaymentMethod = PaymentMethod.Reflect,
                PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0797654321" }
            }
        };

        var result = await _sut.ModifyTicketByTokenAsync("not-a-real-token", request);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task ModifyTicketByTokenAsync_LeavesPaymentUnchanged_WhenNoPaymentProvided()
    {
        var ticket = CreateTicket(pin: "PAY-4", status: TicketSellStatus.ForSale);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);

        var result = await _sut.ModifyTicketByTokenAsync(token, new UpdateTicketRequest());

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        GetPersistedTicket(ticket.Id)!.PaymentMethod.Should().Be(PaymentMethod.Reflect);
    }

    [Test]
    public async Task ModifyTicketByTokenAsync_UpdatesPrice_WhenWithinOriginalPricePlusOne()
    {
        var ticket = CreateTicket(pin: "PRICE-1", status: TicketSellStatus.ForSale, originalPrice: 10m);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);

        var result = await _sut.ModifyTicketByTokenAsync(token, new UpdateTicketRequest { Price = 11m });

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        GetPersistedTicket(ticket.Id)!.TotalPrice.Should().Be(11m);
    }

    [Test]
    public async Task ModifyTicketByTokenAsync_ReturnsBadRequest_WhenPriceExceedsOriginalPricePlusOne()
    {
        var ticket = CreateTicket(pin: "PRICE-2", status: TicketSellStatus.ForSale, originalPrice: 10m);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();
        var token = _deleteTokenService.GenerateToken(ticket.Id);

        var result = await _sut.ModifyTicketByTokenAsync(token, new UpdateTicketRequest { Price = 11.01m });

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        GetPersistedTicket(ticket.Id)!.TotalPrice.Should().Be(10m);
    }
}
