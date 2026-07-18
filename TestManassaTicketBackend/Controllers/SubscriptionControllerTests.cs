using manassa_ticket_backend.Common;
using manassa_ticket_backend.Controllers;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Services.Subscriptions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace TestManassaTicketBackend.Controllers;

public class SubscriptionControllerTests
{
    private Mock<ISubscriptionService> _service = null!;
    private SubscriptionController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new Mock<ISubscriptionService>();
        _sut = new SubscriptionController(_service.Object);
    }

    [Test]
    public async Task Subscribe_ReturnsServiceStatusCodeAndBody()
    {
        var request = new SubscribeRequest { Email = "user@example.com", Date = new DateOnly(2026, 8, 15) };
        var response = new ApiResponse<string> { StatusCode = 200, Success = true, Message = "Subscribed" };
        _service.Setup(s => s.SubscribeAsync(request)).ReturnsAsync(response);

        var result = await _sut.Subscribe(request);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _service.Verify(s => s.SubscribeAsync(request), Times.Once);
    }
}
