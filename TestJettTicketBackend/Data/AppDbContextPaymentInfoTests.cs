using jett_exchange_backend.Common.ValueObjects;
using jett_exchange_backend.Models;
using FluentAssertions;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Data;

public class AppDbContextPaymentInfoTests
{
    private string _databaseName = null!;

    [SetUp]
    public void SetUp() => _databaseName = Guid.NewGuid().ToString();

    private static Ticket CreateTicket(PaymentInfo paymentInfo, PaymentMethod method) => new()
    {
        TicketId = "TCK-1",
        OriginalOwnerName = "Owner",
        OriginalOwnerPassportNumber = "P1",
        NumberOfBags = 1,
        TotalPriceJod = 10m,
        TotalPriceUsd = 14.3m,
        OriginalPrice = 14.3m,
        SellerName = "Seller",
        SellerEmail = "seller@example.com",
        SellerPhone = "+1234567890",
        PaymentMethod = method,
        PaymentInfo = paymentInfo,
        Pin = Guid.NewGuid().ToString("N")[..16],
        Status = TicketSellStatus.ForSale,
        TicketFilePath = "path.pdf"
    };

    [Test]
    public async Task PaymentInfo_RoundTrips_AsBankTransferInfo()
    {
        var ticket = CreateTicket(new BankTransferInfo
        {
            BankDetails = new BankDetails { AccountNumber = "ACC1", BankName = "Bank", Country = "JO", AccountHolderName = "Holder" }
        }, PaymentMethod.Iban);

        using (var writeContext = InMemoryDbContextFactory.Create(_databaseName))
        {
            writeContext.Tickets.Add(ticket);
            await writeContext.SaveChangesAsync();
        }

        using var readContext = InMemoryDbContextFactory.Create(_databaseName);
        var persisted = await readContext.Tickets.FindAsync(ticket.Id);

        persisted!.PaymentInfo.Should().BeOfType<BankTransferInfo>();
        var bankInfo = (BankTransferInfo)persisted.PaymentInfo;
        bankInfo.BankDetails.AccountNumber.Should().Be("ACC1");
        bankInfo.BankDetails.BankName.Should().Be("Bank");
        bankInfo.BankDetails.Country.Should().Be("JO");
        bankInfo.BankDetails.AccountHolderName.Should().Be("Holder");
    }

    [Test]
    public async Task PaymentInfo_RoundTrips_AsReflect()
    {
        var ticket = CreateTicket(new Reflect { PhoneNumber = "0791234567" }, PaymentMethod.Reflect);

        using (var writeContext = InMemoryDbContextFactory.Create(_databaseName))
        {
            writeContext.Tickets.Add(ticket);
            await writeContext.SaveChangesAsync();
        }

        using var readContext = InMemoryDbContextFactory.Create(_databaseName);
        var persisted = await readContext.Tickets.FindAsync(ticket.Id);

        persisted!.PaymentInfo.Should().BeOfType<Reflect>();
        ((Reflect)persisted.PaymentInfo).PhoneNumber.Should().Be("0791234567");
    }

    [Test]
    public async Task PaymentInfo_RoundTrips_AsPhoneTransfer()
    {
        var ticket = CreateTicket(new PhoneTransfer { PhoneNumber = "0797654321" }, PaymentMethod.Phone);

        using (var writeContext = InMemoryDbContextFactory.Create(_databaseName))
        {
            writeContext.Tickets.Add(ticket);
            await writeContext.SaveChangesAsync();
        }

        using var readContext = InMemoryDbContextFactory.Create(_databaseName);
        var persisted = await readContext.Tickets.FindAsync(ticket.Id);

        persisted!.PaymentInfo.Should().BeOfType<PhoneTransfer>();
        ((PhoneTransfer)persisted.PaymentInfo).PhoneNumber.Should().Be("0797654321");
    }

    [Test]
    public void PaymentInfo_SerializesWithTypeDiscriminator()
    {
        var ticket = CreateTicket(new Reflect { PhoneNumber = "0791234567" }, PaymentMethod.Reflect);

        var json = System.Text.Json.JsonSerializer.Serialize<PaymentInfo>(ticket.PaymentInfo, PaymentInfo.JsonOptions);

        json.Should().Contain("\"type\"").And.Contain("reflect").And.Contain("phoneNumber");
    }
}