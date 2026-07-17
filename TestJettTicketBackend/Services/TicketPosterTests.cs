using jett_exchange_backend.Configuration;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Helpers;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.FileStorage;
using jett_exchange_backend.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace TestJettTicketBackend.Services;

public class TicketPosterTests
{
    private Mock<IFileStorage> _storage = null!;
    private Mock<IRandomPinGenerator> _pinGenerator = null!;
    private Mock<ITicketProcessor> _ticketProcessor = null!;
    private TicketPoster _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = new Mock<IFileStorage>();
        _storage.Setup(s => s.CreatePresignedUploadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(("permanent/generated-key.pdf", "https://upload.example/generated-key.pdf"));

        _pinGenerator = new Mock<IRandomPinGenerator>();
        _pinGenerator.Setup(p => p.Generate(It.IsAny<int>())).Returns("GENERATEDPIN");

        _ticketProcessor = new Mock<ITicketProcessor>();

        var options = Options.Create(new StorageOptions { PermanentUploadPath = "permanent" });

        _sut = new TicketPoster(
            options,
            _storage.Object,
            _pinGenerator.Object,
            _ticketProcessor.Object);
    }

    private static PostTicketRequest CreateRequest(string fileKey = "permanent/uploaded.pdf") => new()
    {
        FileKey = fileKey,
        SellerEmail = "seller@example.com",
        SellerPhone = "+1234567890",
        Price = 50m,
        PaymentMethod = PaymentMethod.Reflect,
        PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567" },
        SellerName = "Seller"
    };

    private static Ticket CreateForSaleTicket(Guid id, string pin) => new()
    {
        Id = id,
        TicketId = "TCK-1",
        SellerName = "Seller",
        SellerEmail = "seller@example.com",
        SellerPhone = "+1234567890",
        TotalPriceJod = 100m,
        TotalPriceUsd = 143m,
        PaymentMethod = PaymentMethod.Reflect,
        PaymentInfo = new Reflect { PhoneNumber = "0791234567" },
        Pin = pin,
        Status = TicketSellStatus.ForSale,
        TicketFilePath = "permanent/uploaded.pdf"
    };

    [Test]
    public async Task CreateUploadUrlAsync_ReturnsKeyAndUrl_FromStorage()
    {
        var result = await _sut.CreateUploadUrlAsync();

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data!.FileKey.Should().Be("permanent/generated-key.pdf");
        result.Data!.UploadUrl.Should().Be("https://upload.example/generated-key.pdf");
        _storage.Verify(s => s.CreatePresignedUploadUrlAsync("permanent", It.IsAny<TimeSpan>()), Times.Once);
    }

    [Test]
    public async Task PostTicketAsync_WaitsForProcessing_AndReturnsForSaleResult()
    {
        _ticketProcessor
            .Setup(p => p.ProcessAsync(It.IsAny<TicketSubmission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketSubmission s, CancellationToken _) =>
                new TicketProcessingResult { Success = true, Ticket = CreateForSaleTicket(s.TicketRowId, s.Pin) });

        var request = CreateRequest();

        var result = await _sut.PostTicketAsync(request);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data!.Status.Should().Be(TicketSellStatus.ForSale);
        result.Data!.RefPin.Should().Be("GENERATEDPIN");
        result.Data!.TicketId.Should().NotBeEmpty();
    }

    [Test]
    public async Task PostTicketAsync_PassesRequestedMetadata_ToProcessor()
    {
        _ticketProcessor
            .Setup(p => p.ProcessAsync(It.IsAny<TicketSubmission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketSubmission s, CancellationToken _) =>
                new TicketProcessingResult { Success = true, Ticket = CreateForSaleTicket(s.TicketRowId, s.Pin) });

        var request = CreateRequest(fileKey: "permanent/uploaded.pdf");
        request.Price = 100m;

        await _sut.PostTicketAsync(request);

        _ticketProcessor.Verify(p => p.ProcessAsync(
            It.Is<TicketSubmission>(s =>
                s.Pin == "GENERATEDPIN" &&
                s.TicketFilePath == "permanent/uploaded.pdf" &&
                s.SellerEmail == "seller@example.com" &&
                s.TotalPriceJod == 100m &&
                s.TotalPriceUsd == 143m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PostTicketAsync_ReturnsUnprocessableEntity_WhenProcessingRejectsTheTicket()
    {
        _ticketProcessor
            .Setup(p => p.ProcessAsync(It.IsAny<TicketSubmission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketProcessingResult { Success = false, RejectionReason = "Ticket verification failed." });

        var result = await _sut.PostTicketAsync(CreateRequest());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(422);
        result.Data.Should().BeNull();
        result.Message.Should().Be("Ticket verification failed.");
    }
}
