using jett_exchange_backend.Common.ValueObjects;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.DTOs.TicketExtraction;
using jett_exchange_backend.DTOs.TicketVerification;
using jett_exchange_backend.Helpers;
using jett_exchange_backend.Messaging;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.FileStorage;
using jett_exchange_backend.Services.TicketExtraction;
using jett_exchange_backend.Services.TicketVerification;
using jett_exchange_backend.Services.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Services;

public class TicketPosterTests
{
    private string _databaseName = null!;
    private Mock<IFileStorage> _storage = null!;
    private Mock<ITicketDataExtractor> _extractor = null!;
    private Mock<ITicketVerifier> _verifier = null!;
    private Mock<IRandomPinGenerator> _pinGenerator = null!;
    private Mock<ITicketAvailablePublisher> _availablePublisher = null!;
    private AppDbContext _dbContext = null!;
    private TicketPoster _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = Guid.NewGuid().ToString();
        _dbContext = InMemoryDbContextFactory.Create(_databaseName);

        _storage = new Mock<IFileStorage>();
        _storage.Setup(s => s.SavePdfAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync("saved/path.pdf");
        _storage.Setup(s => s.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);

        _extractor = new Mock<ITicketDataExtractor>();
        _verifier = new Mock<ITicketVerifier>();
        _pinGenerator = new Mock<IRandomPinGenerator>();
        _availablePublisher = new Mock<ITicketAvailablePublisher>();

        var options = Options.Create(new StorageOptions
        {
            TempTicketUploadPath = "temp",
            PermanentUploadPath = "permanent"
        });

        _sut = new TicketPoster(
            _dbContext,
            options,
            _storage.Object,
            _extractor.Object,
            _verifier.Object,
            _pinGenerator.Object,
            _availablePublisher.Object,
            NullLogger<TicketPoster>.Instance);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private Ticket? GetPersistedTicket(Guid id)
    {
        using var readContext = InMemoryDbContextFactory.Create(_databaseName);
        return readContext.Tickets.FirstOrDefault(t => t.Id == id);
    }

    private static PostTicketRequest CreatePostRequest(PaymentMethod method, PaymentInfoRequest paymentInfo) => new()
    {
        File = FakeFormFile.CreatePdf(),
        SellerEmail = "seller@example.com",
        SellerPhone = "+1234567890",
        Price = 50m,
        PaymentMethod = method,
        PaymentInfoRequest = paymentInfo,
        SellerName = "Seller"
    };

    private void SetupSuccessfulExtractionAndVerification(string barcode = "BC123")
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = true, TicketId = "TCK-1", BarCode = barcode });

        _verifier.Setup(v => v.VerifyTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new VerifiedTicketDTO
            {
                Success = true,
                BarCode = barcode,
                OriginalOwnerName = "Owner",
                OriginalOwnerPassportNumber = "P1",
                NumberOfBags = 1,
                TotalPrice = 25m
            });

        _pinGenerator.Setup(p => p.Generate(It.IsAny<int>())).Returns("GENERATEDPIN");
    }

    [Test]
    public async Task PostTicketAsync_SavesTicket_WithBankTransferInfo_WhenIban()
    {
        SetupSuccessfulExtractionAndVerification();
        var request = CreatePostRequest(PaymentMethod.Iban, new PaymentInfoRequest
        {
            BankDetails = new BankDetails { AccountNumber = "111", BankName = "B", Country = "JO", AccountHolderName = "A" }
        });

        var result = await _sut.PostTicketAsync(request);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data!.RefPin.Should().Be("GENERATEDPIN");

        var saved = GetPersistedTicket(result.Data!.TicketId);
        saved.Should().NotBeNull();
        saved!.PaymentMethod.Should().Be(PaymentMethod.Iban);
        saved.PaymentInfo.Should().BeOfType<BankTransferInfo>();
        ((BankTransferInfo)saved.PaymentInfo).BankDetails.AccountNumber.Should().Be("111");
    }

    [Test]
    public async Task PostTicketAsync_SavesTicket_WithReflectInfo_WhenReflect()
    {
        SetupSuccessfulExtractionAndVerification();
        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });

        var result = await _sut.PostTicketAsync(request);

        result.Success.Should().BeTrue();
        var saved = GetPersistedTicket(result.Data!.TicketId);
        saved!.PaymentMethod.Should().Be(PaymentMethod.Reflect);
        saved.PaymentInfo.Should().BeOfType<Reflect>();
        ((Reflect)saved.PaymentInfo).PhoneNumber.Should().Be("0791234567");
    }

    [Test]
    public async Task PostTicketAsync_SavesTicket_WithPhoneTransferInfo_WhenPhone()
    {
        SetupSuccessfulExtractionAndVerification();
        var request = CreatePostRequest(PaymentMethod.Phone, new PaymentInfoRequest { PhoneNumber = "0797654321" });

        var result = await _sut.PostTicketAsync(request);

        result.Success.Should().BeTrue();
        var saved = GetPersistedTicket(result.Data!.TicketId);
        saved!.PaymentMethod.Should().Be(PaymentMethod.Phone);
        saved.PaymentInfo.Should().BeOfType<PhoneTransfer>();
        ((PhoneTransfer)saved.PaymentInfo).PhoneNumber.Should().Be("0797654321");
    }

    [Test]
    public async Task PostTicketAsync_UsesVerifiedTicketData_NotRequestPrice()
    {
        SetupSuccessfulExtractionAndVerification();
        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });
        request.Price = 999m;

        var result = await _sut.PostTicketAsync(request);

        var saved = GetPersistedTicket(result.Data!.TicketId);
        saved!.TotalPrice.Should().Be(25m);
        saved.OriginalPrice.Should().Be(25m);
        saved.OriginalOwnerName.Should().Be("Owner");
    }

    [Test]
    public async Task PostTicketAsync_ReturnsFailure_WhenExtractionUnsuccessful()
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = false, TicketId = "", BarCode = "" });

        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });

        var result = await _sut.PostTicketAsync(request);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        _verifier.Verify(v => v.VerifyTicketAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task PostTicketAsync_ReturnsFailure_WhenVerificationUnsuccessful()
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = true, TicketId = "TCK-1", BarCode = "BC123" });
        _verifier.Setup(v => v.VerifyTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new VerifiedTicketDTO { Success = false });

        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });

        var result = await _sut.PostTicketAsync(request);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.Message.Should().Be("Ticket verification failed");
    }

    [Test]
    public async Task PostTicketAsync_ReturnsFailure_WhenBarcodeMismatch()
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = true, TicketId = "TCK-1", BarCode = "BC123" });
        _verifier.Setup(v => v.VerifyTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new VerifiedTicketDTO { Success = true, BarCode = "DIFFERENT" });

        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });

        var result = await _sut.PostTicketAsync(request);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.Message.Should().Be("Ticket verification failed");
    }

    [Test]
    public async Task PostTicketAsync_ReturnsConflict_WhenTicketAlreadyPosted()
    {
        SetupSuccessfulExtractionAndVerification();
        _dbContext.Tickets.Add(new Ticket
        {
            TicketId = "TCK-1",
            OriginalOwnerName = "Owner",
            OriginalOwnerPassportNumber = "P1",
            NumberOfBags = 1,
            TotalPrice = 25m,
            OriginalPrice = 25m,
            SellerName = "Seller",
            SellerEmail = "seller@example.com",
            SellerPhone = "+1234567890",
            PaymentMethod = PaymentMethod.Reflect,
            PaymentInfo = new Reflect { PhoneNumber = "0791234567" },
            Pin = "EXISTINGPIN",
            Status = TicketSellStatus.ForSale,
            TicketFilePath = "existing/path.pdf"
        });
        await _dbContext.SaveChangesAsync();

        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });

        var result = await _sut.PostTicketAsync(request);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Be("Ticket already posted");
        _storage.Verify(s => s.SavePdfAsync(It.IsAny<IFormFile>(), "permanent"), Times.Never);
        _availablePublisher.Verify(p => p.PublishAsync(It.IsAny<TicketAvailableMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PostTicketAsync_DeletesTempFile_AndUsesCorrectStoragePaths()
    {
        SetupSuccessfulExtractionAndVerification();
        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });

        await _sut.PostTicketAsync(request);

        _storage.Verify(s => s.SavePdfAsync(It.IsAny<IFormFile>(), "temp"), Times.Once);
        _storage.Verify(s => s.SavePdfAsync(It.IsAny<IFormFile>(), "permanent"), Times.Once);
        _storage.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task PostTicketAsync_PublishesTicketAvailableMessage_WithSavedTicketIdAndDate()
    {
        SetupSuccessfulExtractionAndVerification();
        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });

        var result = await _sut.PostTicketAsync(request);

        var saved = GetPersistedTicket(result.Data!.TicketId);
        _availablePublisher.Verify(p => p.PublishAsync(
            It.Is<TicketAvailableMessage>(m => m.TicketId == saved!.Id && m.Date == DateOnly.FromDateTime(saved.TicketDateTime)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PostTicketAsync_DoesNotPublish_WhenExtractionUnsuccessful()
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = false, TicketId = "", BarCode = "" });

        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });

        await _sut.PostTicketAsync(request);

        _availablePublisher.Verify(p => p.PublishAsync(It.IsAny<TicketAvailableMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PostTicketAsync_StillSucceeds_WhenPublishThrows()
    {
        SetupSuccessfulExtractionAndVerification();
        _availablePublisher.Setup(p => p.PublishAsync(It.IsAny<TicketAvailableMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));
        var request = CreatePostRequest(PaymentMethod.Reflect, new PaymentInfoRequest { PhoneNumber = "0791234567" });

        var result = await _sut.PostTicketAsync(request);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
    }
}
