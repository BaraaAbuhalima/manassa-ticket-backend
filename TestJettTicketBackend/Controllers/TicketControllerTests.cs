using jett_exchange_backend.Common;
using jett_exchange_backend.Controllers;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.DTOs.Responses;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Controllers;

public class TicketControllerTests
{
    private Mock<ITicketReader> _reader = null!;
    private Mock<ITicketDeleter> _deleter = null!;
    private Mock<ITicketPoster> _poster = null!;
    private TicketController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _reader = new Mock<ITicketReader>();
        _deleter = new Mock<ITicketDeleter>();
        _poster = new Mock<ITicketPoster>();
        _sut = new TicketController(_reader.Object, _deleter.Object, _poster.Object);
    }

    [Test]
    public async Task GetById_ReturnsServiceStatusCodeAndBody()
    {
        var id = Guid.NewGuid();
        var response = new ApiResponse<GetTicketByIdResponse> { StatusCode = 200, Success = true };
        _reader.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(response);

        var result = await _sut.GetById(id);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _reader.Verify(s => s.GetByIdAsync(id), Times.Once);
    }

    [Test]
    public async Task GetById_PropagatesNotFoundStatusCode()
    {
        var id = Guid.NewGuid();
        var response = new ApiResponse<GetTicketByIdResponse> { StatusCode = 404, Success = false };
        _reader.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(response);

        var result = await _sut.GetById(id);

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task GetByPin_ReturnsServiceStatusCodeAndBody()
    {
        var response = new ApiResponse<Ticket> { StatusCode = 200, Success = true };
        _reader.Setup(s => s.GetByPinAsync("ABC123")).ReturnsAsync(response);

        var result = await _sut.GetByPin("ABC123");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _reader.Verify(s => s.GetByPinAsync("ABC123"), Times.Once);
    }

    [Test]
    public async Task GetForDate_ReturnsServiceStatusCodeAndBody()
    {
        var date = new DateOnly(2026, 7, 5);
        var response = new ApiResponse<List<Ticket>> { StatusCode = 200, Success = true, Data = [] };
        _reader.Setup(s => s.GetForDateAsync(date, 1)).ReturnsAsync(response);

        var result = await _sut.GetForDate(date);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _reader.Verify(s => s.GetForDateAsync(date, 1), Times.Once);
    }

    [Test]
    public async Task GetForDate_PassesRequestedPage()
    {
        var date = new DateOnly(2026, 7, 5);
        var response = new ApiResponse<List<Ticket>> { StatusCode = 200, Success = true, Data = [] };
        _reader.Setup(s => s.GetForDateAsync(date, 3)).ReturnsAsync(response);

        await _sut.GetForDate(date, page: 3);

        _reader.Verify(s => s.GetForDateAsync(date, 3), Times.Once);
    }

    [Test]
    public async Task GetForDateRange_ReturnsServiceStatusCodeAndBody()
    {
        var start = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2026, 7, 10);
        var response = new ApiResponse<List<Ticket>> { StatusCode = 200, Success = true, Data = [] };
        _reader.Setup(s => s.GetForDateRangeAsync(start, end, 1)).ReturnsAsync(response);

        var result = await _sut.GetForDateRange(start, end);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _reader.Verify(s => s.GetForDateRangeAsync(start, end, 1), Times.Once);
    }

    [Test]
    public async Task GetForDateRange_PropagatesBadRequestStatusCode()
    {
        var start = new DateOnly(2026, 7, 10);
        var end = new DateOnly(2026, 7, 1);
        var response = new ApiResponse<List<Ticket>> { StatusCode = 400, Success = false };
        _reader.Setup(s => s.GetForDateRangeAsync(start, end, 1)).ReturnsAsync(response);

        var result = await _sut.GetForDateRange(start, end);

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task DeleteByToken_ReturnsServiceStatusCodeAndBody()
    {
        var response = new ApiResponse<string> { StatusCode = 200, Success = true };
        _deleter.Setup(s => s.DeleteByTokenAsync("valid-token")).ReturnsAsync(response);

        var result = await _sut.DeleteByToken("valid-token");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _deleter.Verify(s => s.DeleteByTokenAsync("valid-token"), Times.Once);
    }

    [Test]
    public async Task DeleteByToken_PropagatesNotFoundStatusCode()
    {
        var response = new ApiResponse<string> { StatusCode = 404, Success = false };
        _deleter.Setup(s => s.DeleteByTokenAsync("missing-ticket-token")).ReturnsAsync(response);

        var result = await _sut.DeleteByToken("missing-ticket-token");

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task DeleteByToken_PropagatesUnauthorizedStatusCode_ForInvalidToken()
    {
        var response = new ApiResponse<string> { StatusCode = 401, Success = false };
        _deleter.Setup(s => s.DeleteByTokenAsync("bad-token")).ReturnsAsync(response);

        var result = await _sut.DeleteByToken("bad-token");

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task PostTicket_ReturnsServiceStatusCodeAndBody()
    {
        var request = new PostTicketRequest
        {
            File = FakeFormFile.CreatePdf(),
            SellerEmail = "seller@example.com",
            SellerPhone = "+1234567890",
            Price = 10m,
            PaymentMethod = PaymentMethod.Reflect,
            PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567" },
            SellerName = "Seller"
        };
        var response = new ApiResponse<PostTicketResponse>
        {
            StatusCode = 200,
            Success = true,
            Data = new PostTicketResponse { TicketId = Guid.NewGuid(), RefPin = "PIN" }
        };
        _poster.Setup(s => s.PostTicketAsync(request)).ReturnsAsync(response);

        var result = await _sut.PostTicket(request);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _poster.Verify(s => s.PostTicketAsync(request), Times.Once);
    }

    [Test]
    public async Task PostTicket_PropagatesFailureStatusCode()
    {
        var request = new PostTicketRequest
        {
            File = FakeFormFile.CreatePdf(),
            SellerEmail = "seller@example.com",
            SellerPhone = "+1234567890",
            Price = 10m,
            PaymentMethod = PaymentMethod.Reflect,
            PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567" },
            SellerName = "Seller"
        };
        var response = new ApiResponse<PostTicketResponse> { StatusCode = 500, Success = false };
        _poster.Setup(s => s.PostTicketAsync(request)).ReturnsAsync(response);

        var result = await _sut.PostTicket(request);

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(500);
    }
}
