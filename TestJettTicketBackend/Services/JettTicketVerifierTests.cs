using System.Net;
using System.Text;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.Services.TicketVerification;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Services;

public class JettTicketVerifierTests
{
    private const string ValidTransferTicketJson = """
    {
      "Status": "success",
      "Details": {
        "Status": "success",
        "Data": {
          "Id": 1,
          "Booking_id": 1,
          "Type": 1,
          "Name": "John Doe",
          "Passport_number": "P1234567",
          "Count_luggage": 2,
          "Is_additional_luggage": 0,
          "Related_transaction_id": 0,
          "Ticket_id": "T1",
          "Master_ticket_id": "M1",
          "Ticket_amount": "25.50",
          "Barcode": "BC123",
          "Ticket_pdf": "pdf",
          "Sent_whatsapp_at": "",
          "Sent_email_at": "",
          "Created_at": "",
          "Updated_at": "",
          "Booking": {
            "Id": 1,
            "User_id": 1,
            "Trip_id": 1,
            "Booking_type": 1,
            "Adults": 1,
            "Payment_ref": "ref",
            "Travel_date": "2026-08-15",
            "Travel_time_from": "14:30:00",
            "Travel_time_to": "16:00:00",
            "Booking_date": "2026-08-01",
            "User_email": "user@example.com",
            "User_phone": "+1234567890",
            "User_name": "John Doe",
            "Third_party_status": 0,
            "Created_at": "",
            "Updated_at": ""
          }
        }
      }
    }
    """;

    private static JettTicketVerifier CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond, out FakeHttpMessageHandler handlerRef)
    {
        var options = Options.Create(new JettApiOptions { BaseUrl = "https://jett-khb.com.jo/api/" });
        var handler = new FakeHttpMessageHandler(respond);
        handlerRef = handler;
        var client = new HttpClient(handler);
        return new JettTicketVerifier(client, options, NullLogger<JettTicketVerifier>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    [Test]
    public async Task VerifyTicketAsync_ReturnsVerifiedTicket_OnSuccess()
    {
        var sut = CreateSut(_ => JsonResponse(HttpStatusCode.OK, ValidTransferTicketJson), out _);

        var result = await sut.VerifyTicketAsync("BC123");

        result.Success.Should().BeTrue();
        result.BarCode.Should().Be("BC123");
        result.OriginalOwnerName.Should().Be("John Doe");
        result.OriginalOwnerPassportNumber.Should().Be("P1234567");
        result.NumberOfBags.Should().Be(2);
    }

    [Test]
    public async Task VerifyTicketAsync_ReturnsUnsuccessful_WhenResponseBodyIsNull()
    {
        var sut = CreateSut(_ => JsonResponse(HttpStatusCode.OK, "null"), out _);

        var result = await sut.VerifyTicketAsync("BC123");

        result.Success.Should().BeFalse();
    }

    [Test]
    public async Task VerifyTicketAsync_SendsBarcodeAndLanguage_ToExpectedRoute()
    {
        var sut = CreateSut(_ => JsonResponse(HttpStatusCode.OK, ValidTransferTicketJson), out var handler);

        await sut.VerifyTicketAsync("BC999");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/booking/ticket-details-by-barcode");

        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        body.Should().Contain("BC999").And.Contain("en");
    }

    [Test]
    public async Task VerifyTicketAsync_ReturnsUnsuccessful_WhenApiReturnsNonSuccessStatusCode()
    {
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden), out _);

        var result = await sut.VerifyTicketAsync("BC123");

        result.Success.Should().BeFalse();
    }

    [Test]
    public async Task VerifyTicketAsync_ReturnsUnsuccessful_WhenResponseBodyIsMalformedJson()
    {
        var sut = CreateSut(_ => JsonResponse(HttpStatusCode.OK, "{not-valid-json"), out _);

        var result = await sut.VerifyTicketAsync("BC123");

        result.Success.Should().BeFalse();
    }
}
