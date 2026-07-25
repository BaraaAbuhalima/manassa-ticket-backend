using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Messaging;
using manassa_ticket_backend.Services.Contact;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace TestManassaTicketBackend.Services;

public class ContactUsServiceTests
{
    private Mock<IEmailMessagePublisher> _emailPublisher = null!;
    private ContactUsService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _emailPublisher = new Mock<IEmailMessagePublisher>();
        var options = Options.Create(new ContactOptions { RecipientEmail = "owner@example.com" });
        _sut = new ContactUsService(_emailPublisher.Object, options);
    }

    [Test]
    public async Task SubmitAsync_PublishesEmailMessage_ToConfiguredRecipient()
    {
        var request = new ContactUsRequest { Name = "Jane Doe", Email = "jane@example.com", Message = "Hello there" };

        await _sut.SubmitAsync(request);

        _emailPublisher.Verify(e => e.PublishAsync(
            It.Is<SendEmailMessage>(m => m.To == "owner@example.com"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SubmitAsync_IncludesSenderNameAndMessage_InEmailBody()
    {
        var request = new ContactUsRequest { Name = "Jane Doe", Email = "jane@example.com", Message = "Hello there" };

        await _sut.SubmitAsync(request);

        _emailPublisher.Verify(e => e.PublishAsync(
            It.Is<SendEmailMessage>(m =>
                m.Subject.Contains("Jane Doe") &&
                m.Body.Contains("jane@example.com") &&
                m.Body.Contains("Hello there")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SubmitAsync_SendsConfirmationEmail_ToSubmitter()
    {
        var request = new ContactUsRequest { Name = "Jane Doe", Email = "jane@example.com", Message = "Hello there" };

        await _sut.SubmitAsync(request);

        _emailPublisher.Verify(e => e.PublishAsync(
            It.Is<SendEmailMessage>(m => m.To == "jane@example.com" && m.Body.Contains("Hello there")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SubmitAsync_ReturnsSuccessResponse()
    {
        var request = new ContactUsRequest { Name = "Jane Doe", Email = "jane@example.com", Message = "Hello there" };

        var result = await _sut.SubmitAsync(request);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
    }
}
