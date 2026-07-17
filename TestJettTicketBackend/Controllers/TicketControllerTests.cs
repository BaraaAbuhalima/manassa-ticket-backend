using jett_exchange_backend.Common;
using jett_exchange_backend.Controllers;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.DTOs.Responses;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace TestJettTicketBackend.Controllers;

public class TicketControllerTests
{
    private Mock<ITicketReader> _reader = null!;
    private Mock<ITicketDeleter> _deleter = null!;
    private Mock<ITicketPoster> _poster = null!;
    private Mock<ITicketDeleteTokenService> _deleteTokenService = null!;
    private TicketController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _reader = new Mock<ITicketReader>();
        _deleter = new Mock<ITicketDeleter>();
        _poster = new Mock<ITicketPoster>();
        _deleteTokenService = new Mock<ITicketDeleteTokenService>();
        _sut = new TicketController(_reader.Object, _deleter.Object, _poster.Object, _deleteTokenService.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private void SetAuthorizationHeader(string value) =>
        _sut.ControllerContext.HttpContext.Request.Headers.Authorization = value;

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
        var response = new ApiResponse<GetTicketByPinResponse> { StatusCode = 200, Success = true };
        _reader.Setup(s => s.GetByPinAsync("ABC123")).ReturnsAsync(response);

        var result = await _sut.GetByPin("ABC123");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _reader.Verify(s => s.GetByPinAsync("ABC123"), Times.Once);
    }

    [Test]
    public async Task GetByPin_SetsDeleteTokenResponseHeader_WhenTicketFound()
    {
        var ticketId = Guid.NewGuid();
        var response = new ApiResponse<GetTicketByPinResponse>
        {
            StatusCode = 200,
            Success = true,
            Data = new GetTicketByPinResponse
            {
                Id = ticketId,
                TicketDateTime = DateTime.UtcNow,
                NumberOfBags = 1,
                TotalPriceUsd = 14.3m,
                TotalPriceJod = 10m,
                Status = TicketSellStatus.ForSale,
                SellerEmail = "seller@example.com",
                SellerPhone = "+1234567890",
                PaymentMethod = PaymentMethod.Reflect,
                PaymentInfo = new Reflect { PhoneNumber = "0791234567" }
            }
        };
        _reader.Setup(s => s.GetByPinAsync("ABC123")).ReturnsAsync(response);
        _deleteTokenService.Setup(s => s.GenerateToken(ticketId)).Returns("the-delete-jwt");

        await _sut.GetByPin("ABC123");

        _sut.Response.Headers["X-Delete-Token"].ToString().Should().Be("the-delete-jwt");
    }

    [Test]
    public async Task GetByPin_DoesNotSetDeleteTokenResponseHeader_WhenTicketNotFound()
    {
        var response = new ApiResponse<GetTicketByPinResponse> { StatusCode = 404, Success = false };
        _reader.Setup(s => s.GetByPinAsync("missing")).ReturnsAsync(response);

        await _sut.GetByPin("missing");

        _sut.Response.Headers.Should().NotContainKey("X-Delete-Token");
    }

    [Test]
    public async Task GetForDate_ReturnsServiceStatusCodeAndBody()
    {
        var date = new DateOnly(2026, 7, 5);
        var response = new ApiResponse<List<GetTicketByIdResponse>> { StatusCode = 200, Success = true, Data = [] };
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
        var response = new ApiResponse<List<GetTicketByIdResponse>> { StatusCode = 200, Success = true, Data = [] };
        _reader.Setup(s => s.GetForDateAsync(date, 3)).ReturnsAsync(response);

        await _sut.GetForDate(date, page: 3);

        _reader.Verify(s => s.GetForDateAsync(date, 3), Times.Once);
    }

    [Test]
    public async Task GetForDateRange_ReturnsServiceStatusCodeAndBody()
    {
        var start = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2026, 7, 10);
        var response = new ApiResponse<List<GetTicketByIdResponse>> { StatusCode = 200, Success = true, Data = [] };
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
        var response = new ApiResponse<List<GetTicketByIdResponse>> { StatusCode = 400, Success = false };
        _reader.Setup(s => s.GetForDateRangeAsync(start, end, 1)).ReturnsAsync(response);

        var result = await _sut.GetForDateRange(start, end);

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task DeleteByToken_ReturnsServiceStatusCodeAndBody()
    {
        SetAuthorizationHeader("Bearer valid-token");
        var response = new ApiResponse<string> { StatusCode = 200, Success = true };
        _deleter.Setup(s => s.DeleteByTokenAsync("valid-token")).ReturnsAsync(response);

        var result = await _sut.DeleteByToken();

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _deleter.Verify(s => s.DeleteByTokenAsync("valid-token"), Times.Once);
    }

    [Test]
    public async Task DeleteByToken_PropagatesNotFoundStatusCode()
    {
        SetAuthorizationHeader("Bearer missing-ticket-token");
        var response = new ApiResponse<string> { StatusCode = 404, Success = false };
        _deleter.Setup(s => s.DeleteByTokenAsync("missing-ticket-token")).ReturnsAsync(response);

        var result = await _sut.DeleteByToken();

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task DeleteByToken_PropagatesUnauthorizedStatusCode_ForInvalidToken()
    {
        SetAuthorizationHeader("Bearer bad-token");
        var response = new ApiResponse<string> { StatusCode = 401, Success = false };
        _deleter.Setup(s => s.DeleteByTokenAsync("bad-token")).ReturnsAsync(response);

        var result = await _sut.DeleteByToken();

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task DeleteByToken_ReadsTokenFromAuthorizationHeader_CaseInsensitiveBearerPrefix()
    {
        SetAuthorizationHeader("bearer case-insensitive-token");
        var response = new ApiResponse<string> { StatusCode = 200, Success = true };
        _deleter.Setup(s => s.DeleteByTokenAsync("case-insensitive-token")).ReturnsAsync(response);

        await _sut.DeleteByToken();

        _deleter.Verify(s => s.DeleteByTokenAsync("case-insensitive-token"), Times.Once);
    }

    [Test]
    public async Task DeleteByToken_PassesEmptyToken_WhenAuthorizationHeaderMissing()
    {
        var response = new ApiResponse<string> { StatusCode = 401, Success = false };
        _deleter.Setup(s => s.DeleteByTokenAsync(string.Empty)).ReturnsAsync(response);

        await _sut.DeleteByToken();

        _deleter.Verify(s => s.DeleteByTokenAsync(string.Empty), Times.Once);
    }

    [Test]
    public async Task DeleteByToken_PassesEmptyToken_WhenAuthorizationHeaderMissingBearerPrefix()
    {
        SetAuthorizationHeader("just-the-token-no-prefix");
        var response = new ApiResponse<string> { StatusCode = 401, Success = false };
        _deleter.Setup(s => s.DeleteByTokenAsync(string.Empty)).ReturnsAsync(response);

        await _sut.DeleteByToken();

        _deleter.Verify(s => s.DeleteByTokenAsync(string.Empty), Times.Once);
    }

    [Test]
    public async Task RepublishByToken_ReturnsServiceStatusCodeAndBody()
    {
        SetAuthorizationHeader("Bearer valid-token");
        var response = new ApiResponse<string> { StatusCode = 200, Success = true };
        _deleter.Setup(s => s.RepublishByTokenAsync("valid-token")).ReturnsAsync(response);

        var result = await _sut.RepublishByToken();

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _deleter.Verify(s => s.RepublishByTokenAsync("valid-token"), Times.Once);
    }

    [Test]
    public async Task RepublishByToken_PropagatesConflictStatusCode()
    {
        SetAuthorizationHeader("Bearer valid-token");
        var response = new ApiResponse<string> { StatusCode = 409, Success = false };
        _deleter.Setup(s => s.RepublishByTokenAsync("valid-token")).ReturnsAsync(response);

        var result = await _sut.RepublishByToken();

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(409);
    }

    [Test]
    public async Task RepublishByToken_PassesEmptyToken_WhenAuthorizationHeaderMissing()
    {
        var response = new ApiResponse<string> { StatusCode = 401, Success = false };
        _deleter.Setup(s => s.RepublishByTokenAsync(string.Empty)).ReturnsAsync(response);

        await _sut.RepublishByToken();

        _deleter.Verify(s => s.RepublishByTokenAsync(string.Empty), Times.Once);
    }

    [Test]
    public async Task ModifyTicket_ReturnsServiceStatusCodeAndBody()
    {
        SetAuthorizationHeader("Bearer valid-token");
        var request = new UpdateTicketRequest
        {
            Payment = new PaymentUpdateRequest
            {
                PaymentMethod = PaymentMethod.Reflect,
                PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567" }
            }
        };
        var response = new ApiResponse<string> { StatusCode = 200, Success = true };
        _deleter.Setup(s => s.ModifyTicketByTokenAsync("valid-token", request)).ReturnsAsync(response);

        var result = await _sut.ModifyTicket(request);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _deleter.Verify(s => s.ModifyTicketByTokenAsync("valid-token", request), Times.Once);
    }

    [Test]
    public async Task ModifyTicket_PropagatesConflictStatusCode()
    {
        SetAuthorizationHeader("Bearer valid-token");
        var request = new UpdateTicketRequest
        {
            Payment = new PaymentUpdateRequest
            {
                PaymentMethod = PaymentMethod.Reflect,
                PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567" }
            }
        };
        var response = new ApiResponse<string> { StatusCode = 409, Success = false };
        _deleter.Setup(s => s.ModifyTicketByTokenAsync("valid-token", request)).ReturnsAsync(response);

        var result = await _sut.ModifyTicket(request);

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(409);
    }

    [Test]
    public async Task ModifyTicket_PassesEmptyToken_WhenAuthorizationHeaderMissing()
    {
        var request = new UpdateTicketRequest
        {
            Payment = new PaymentUpdateRequest
            {
                PaymentMethod = PaymentMethod.Reflect,
                PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567" }
            }
        };
        var response = new ApiResponse<string> { StatusCode = 401, Success = false };
        _deleter.Setup(s => s.ModifyTicketByTokenAsync(string.Empty, request)).ReturnsAsync(response);

        await _sut.ModifyTicket(request);

        _deleter.Verify(s => s.ModifyTicketByTokenAsync(string.Empty, request), Times.Once);
    }

    [Test]
    public async Task PostTicket_ReturnsServiceStatusCodeAndBody()
    {
        var request = new PostTicketRequest
        {
            FileKey = "permanent/abc.pdf",
            SellerEmail = "seller@example.com",
            SellerPhone = "+1234567890",
            Price = 10m,
            PaymentMethod = PaymentMethod.Reflect,
            PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567" },
            SellerName = "Seller"
        };
        var response = new ApiResponse<PostTicketResponse>
        {
            StatusCode = 202,
            Success = true,
            Data = new PostTicketResponse { TicketId = Guid.NewGuid(), RefPin = "PIN", Status = TicketSellStatus.Processing }
        };
        _poster.Setup(s => s.PostTicketAsync(request)).ReturnsAsync(response);

        var result = await _sut.PostTicket(request);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(202);
        objectResult.Value.Should().BeSameAs(response);
        _poster.Verify(s => s.PostTicketAsync(request), Times.Once);
    }

    [Test]
    public async Task PostTicket_PropagatesFailureStatusCode()
    {
        var request = new PostTicketRequest
        {
            FileKey = "permanent/abc.pdf",
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

    [Test]
    public async Task CreateUploadUrl_ReturnsServiceStatusCodeAndBody()
    {
        var response = new ApiResponse<CreateUploadUrlResponse>
        {
            StatusCode = 200,
            Success = true,
            Data = new CreateUploadUrlResponse { FileKey = "permanent/abc.pdf", UploadUrl = "https://r2.example/upload" }
        };
        _poster.Setup(s => s.CreateUploadUrlAsync()).ReturnsAsync(response);

        var result = await _sut.CreateUploadUrl();

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _poster.Verify(s => s.CreateUploadUrlAsync(), Times.Once);
    }

    [Test]
    public async Task GetFileUrl_ReturnsServiceStatusCodeAndBody()
    {
        SetAuthorizationHeader("Bearer valid-token");
        var response = new ApiResponse<TicketFileUrlResponse>
        {
            StatusCode = 200,
            Success = true,
            Data = new TicketFileUrlResponse { DownloadUrl = "https://r2.example/download" }
        };
        _deleter.Setup(s => s.GetFileUrlByTokenAsync("valid-token")).ReturnsAsync(response);

        var result = await _sut.GetFileUrl();

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeSameAs(response);
        _deleter.Verify(s => s.GetFileUrlByTokenAsync("valid-token"), Times.Once);
    }

    [Test]
    public async Task GetFileUrl_PropagatesUnauthorizedStatusCode_ForInvalidToken()
    {
        SetAuthorizationHeader("Bearer bad-token");
        var response = new ApiResponse<TicketFileUrlResponse> { StatusCode = 401, Success = false };
        _deleter.Setup(s => s.GetFileUrlByTokenAsync("bad-token")).ReturnsAsync(response);

        var result = await _sut.GetFileUrl();

        result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(401);
    }
}
