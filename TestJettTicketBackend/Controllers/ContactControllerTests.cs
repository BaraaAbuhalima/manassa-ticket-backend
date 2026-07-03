using jett_exchange_backend.Common;
using jett_exchange_backend.Controllers;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Services.Contact;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace TestJettTicketBackend.Controllers;

public class ContactControllerTests
{
    private Mock<IContactUsService> _service = null!;
    private ContactController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new Mock<IContactUsService>();
        _sut = new ContactController(_service.Object);
    }

    [Test]
    public async Task ContactUs_ReturnsServiceStatusCodeAndBody()
    {
        var request = new ContactUsRequest { Name = "Jane Doe", Email = "jane@example.com", Message = "Hello there" };
        var response = new ApiResponse<string> { StatusCode = 200, Success = true, Message = "Sent" };
        _service.Setup(s => s.SubmitAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _sut.ContactUs(request, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _service.Verify(s => s.SubmitAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}
