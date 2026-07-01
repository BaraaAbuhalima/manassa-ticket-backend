using System.Net;
using System.Text;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.Services.TicketExtraction;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Services;

public class PythonTicketDataExtractorTests
{
    private static PythonTicketDataExtractor CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond, out FakeHttpMessageHandler handlerRef)
    {
        var options = Options.Create(new PythonExtractorOptions { BaseUrl = "http://python-service/" });
        var handler = new FakeHttpMessageHandler(respond);
        handlerRef = handler;
        var client = new HttpClient(handler);
        return new PythonTicketDataExtractor(client, options);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    [Test]
    public async Task ExtractTicketAsync_ReturnsDeserializedResult_OnSuccess()
    {
        var sut = CreateSut(_ => JsonResponse(HttpStatusCode.OK,
            """{"Success":true,"TicketId":"TCK-1","BarCode":"BC123"}"""), out _);

        var result = await sut.ExtractTicketAsync("/tmp/ticket.pdf");

        result.Success.Should().BeTrue();
        result.TicketId.Should().Be("TCK-1");
        result.BarCode.Should().Be("BC123");
    }

    [Test]
    public async Task ExtractTicketAsync_UrlEncodesFilePath_AndPostsToExpectedRoute()
    {
        var sut = CreateSut(_ => JsonResponse(HttpStatusCode.OK,
            """{"Success":true,"TicketId":"T","BarCode":"B"}"""), out var handler);

        await sut.ExtractTicketAsync("/tmp/some folder/ticket.pdf");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            "/extract-ticket-pdf-info?file_path=%2Ftmp%2Fsome%20folder%2Fticket.pdf");
    }

    [Test]
    public void ExtractTicketAsync_Throws_WhenServiceReturnsNonSuccessStatusCode()
    {
        // Documents current behavior: the extractor does not catch HTTP failures,
        // so a non-2xx response from the Python service surfaces as an unhandled
        // HttpRequestException rather than a graceful PdfTicketDTO { Success = false }.
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError), out _);

        var act = async () => await sut.ExtractTicketAsync("/tmp/ticket.pdf");

        act.Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    public void ExtractTicketAsync_Throws_WhenResponseUsesCamelCaseKeys()
    {
        // Documents current behavior: deserialization uses plain JsonSerializer.Deserialize
        // with no options, which is case-sensitive PascalCase-only. A camelCase payload
        // (the more common JSON convention) fails instead of binding leniently.
        var sut = CreateSut(_ => JsonResponse(HttpStatusCode.OK,
            """{"success":true,"ticketId":"TCK-1","barCode":"BC123"}"""), out _);

        var act = async () => await sut.ExtractTicketAsync("/tmp/ticket.pdf");

        act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }
}
