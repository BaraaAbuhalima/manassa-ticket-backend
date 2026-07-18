namespace TestManassaTicketBackend.TestHelpers;

public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(respond(request));
    }

    public static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        return new HttpClient(new FakeHttpMessageHandler(respond));
    }
}