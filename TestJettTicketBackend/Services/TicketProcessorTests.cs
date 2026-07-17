using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.TicketExtraction;
using jett_exchange_backend.DTOs.TicketVerification;
using jett_exchange_backend.Messaging;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.FileStorage;
using jett_exchange_backend.Services.TicketExtraction;
using jett_exchange_backend.Services.TicketVerification;
using jett_exchange_backend.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Services;

public class TicketProcessorTests
{
    private string _databaseName = null!;
    private AppDbContext _dbContext = null!;
    private Mock<IFileStorage> _storage = null!;
    private Mock<ITicketDataExtractor> _extractor = null!;
    private Mock<ITicketVerifier> _verifier = null!;
    private Mock<ITicketAvailablePublisher> _availablePublisher = null!;
    private Mock<IEmailMessagePublisher> _emailPublisher = null!;
    private TicketProcessor _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = Guid.NewGuid().ToString();
        _dbContext = InMemoryDbContextFactory.Create(_databaseName);

        _storage = new Mock<IFileStorage>();
        _storage.Setup(s => s.OpenReadAsync(It.IsAny<string>())).ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        _storage.Setup(s => s.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);

        _extractor = new Mock<ITicketDataExtractor>();
        _verifier = new Mock<ITicketVerifier>();
        _availablePublisher = new Mock<ITicketAvailablePublisher>();
        _emailPublisher = new Mock<IEmailMessagePublisher>();

        _sut = new TicketProcessor(
            _dbContext,
            _storage.Object,
            _extractor.Object,
            _verifier.Object,
            _availablePublisher.Object,
            _emailPublisher.Object,
            NullLogger<TicketProcessor>.Instance);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private static TicketSubmission CreateSubmission(string pin = "PIN123", string filePath = "permanent/file.pdf") => new()
    {
        TicketRowId = Guid.NewGuid(),
        SellerName = "Seller",
        SellerEmail = "seller@example.com",
        SellerPhone = "+1234567890",
        TotalPriceJod = 50m,
        TotalPriceUsd = 71.5m,
        PaymentMethod = PaymentMethod.Reflect,
        PaymentInfo = new Reflect { PhoneNumber = "0791234567" },
        Pin = pin,
        TicketFilePath = filePath
    };

    private Ticket? GetPersistedTicket(Guid id)
    {
        using var readContext = InMemoryDbContextFactory.Create(_databaseName);
        return readContext.Tickets.FirstOrDefault(t => t.Id == id);
    }

    private void SetupSuccessfulExtractionAndVerification(string barcode = "BC123")
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<Stream>(), It.IsAny<string>()))
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
    }

    [Test]
    public async Task ProcessAsync_ReturnsForSaleTicket_WithExtractedAndVerifiedData_OnSuccess()
    {
        SetupSuccessfulExtractionAndVerification();
        var submission = CreateSubmission();

        var result = await _sut.ProcessAsync(submission);

        result.Success.Should().BeTrue();
        result.Ticket!.Status.Should().Be(TicketSellStatus.ForSale);
        result.Ticket.TicketId.Should().Be("TCK-1");
        result.Ticket.OriginalOwnerName.Should().Be("Owner");
        result.Ticket.OriginalOwnerPassportNumber.Should().Be("P1");
        result.Ticket.NumberOfBags.Should().Be(1);
        result.Ticket.OriginalPrice.Should().Be(25m);
        result.Ticket.SellerEmail.Should().Be("seller@example.com");
        result.Ticket.Pin.Should().Be("PIN123");

        var saved = GetPersistedTicket(submission.TicketRowId);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(TicketSellStatus.ForSale);
    }

    [Test]
    public async Task ProcessAsync_PublishesTicketAvailableAndConfirmationEmail_OnSuccess()
    {
        SetupSuccessfulExtractionAndVerification();
        var submission = CreateSubmission();

        var result = await _sut.ProcessAsync(submission);

        _availablePublisher.Verify(p => p.PublishAsync(
            It.Is<TicketAvailableMessage>(m => m.TicketId == result.Ticket!.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailPublisher.Verify(p => p.PublishAsync(
            It.Is<SendEmailMessage>(m => m.To == "seller@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessAsync_RejectsWithoutPersisting_WhenExtractionUnsuccessful()
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = false, TicketId = "", BarCode = "" });
        var submission = CreateSubmission();

        var result = await _sut.ProcessAsync(submission);

        result.Success.Should().BeFalse();
        result.RejectionReason.Should().NotBeNullOrEmpty();
        GetPersistedTicket(submission.TicketRowId).Should().BeNull();
        _verifier.Verify(v => v.VerifyTicketAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ProcessAsync_RejectsWithoutPersisting_WhenVerificationUnsuccessful()
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = true, TicketId = "TCK-1", BarCode = "BC123" });
        _verifier.Setup(v => v.VerifyTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new VerifiedTicketDTO { Success = false });
        var submission = CreateSubmission();

        var result = await _sut.ProcessAsync(submission);

        result.Success.Should().BeFalse();
        GetPersistedTicket(submission.TicketRowId).Should().BeNull();
    }

    [Test]
    public async Task ProcessAsync_RejectsWithoutPersisting_WhenBarcodeMismatch()
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = true, TicketId = "TCK-1", BarCode = "BC123" });
        _verifier.Setup(v => v.VerifyTicketAsync(It.IsAny<string>()))
            .ReturnsAsync(new VerifiedTicketDTO { Success = true, BarCode = "DIFFERENT" });
        var submission = CreateSubmission();

        var result = await _sut.ProcessAsync(submission);

        result.Success.Should().BeFalse();
        GetPersistedTicket(submission.TicketRowId).Should().BeNull();
    }

    [Test]
    public async Task ProcessAsync_RejectsWithoutTouchingExistingTicket_WhenAlreadyPosted()
    {
        SetupSuccessfulExtractionAndVerification();
        var existing = new Ticket
        {
            Id = Guid.NewGuid(),
            SellerName = "Other Seller",
            SellerEmail = "other@example.com",
            SellerPhone = "+1234567890",
            TotalPriceJod = 50m,
            TotalPriceUsd = 71.5m,
            PaymentMethod = PaymentMethod.Reflect,
            PaymentInfo = new Reflect { PhoneNumber = "0791234567" },
            Pin = "EXISTING",
            Status = TicketSellStatus.ForSale,
            TicketFilePath = "permanent/existing.pdf",
            TicketId = "TCK-1"
        };
        _dbContext.Tickets.Add(existing);
        await _dbContext.SaveChangesAsync();

        var submission = CreateSubmission(pin: "NEW");

        var result = await _sut.ProcessAsync(submission);

        result.Success.Should().BeFalse();
        GetPersistedTicket(submission.TicketRowId).Should().BeNull();
        GetPersistedTicket(existing.Id)!.Status.Should().Be(TicketSellStatus.ForSale);
        _availablePublisher.Verify(p => p.PublishAsync(It.IsAny<TicketAvailableMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessAsync_DeletesFileFromStorage_WhenRejected()
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = false, TicketId = "", BarCode = "" });
        var submission = CreateSubmission(filePath: "permanent/file.pdf");

        await _sut.ProcessAsync(submission);

        _storage.Verify(s => s.DeleteAsync(submission.TicketFilePath), Times.Once);
    }

    [Test]
    public async Task ProcessAsync_SendsRejectionEmail_WhenRejected()
    {
        _extractor.Setup(e => e.ExtractTicketAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new PdfTicketDTO { Success = false, TicketId = "", BarCode = "" });
        var submission = CreateSubmission();

        await _sut.ProcessAsync(submission);

        _emailPublisher.Verify(p => p.PublishAsync(
            It.Is<SendEmailMessage>(m => m.To == "seller@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessAsync_RejectsWithoutPersisting_WhenFileMissingFromStorage()
    {
        _storage.Setup(s => s.OpenReadAsync(It.IsAny<string>())).ReturnsAsync((Stream?)null);
        var submission = CreateSubmission();

        var result = await _sut.ProcessAsync(submission);

        result.Success.Should().BeFalse();
        GetPersistedTicket(submission.TicketRowId).Should().BeNull();
        _extractor.Verify(e => e.ExtractTicketAsync(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
    }
}
