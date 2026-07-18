using System.Net.Http.Headers;
using System.Text.Json;
using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.DTOs.TicketExtraction;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.TicketExtraction;

public class PythonTicketDataExtractor : ITicketDataExtractor
{
    private readonly HttpClient _client;

    public PythonTicketDataExtractor(
        HttpClient httpClient,
        IOptions<PythonExtractorOptions> options)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri(options.Value.BaseUrl);
    }

    public async Task<PdfTicketDTO> ExtractTicketAsync(Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", fileName);

        var response = await _client.PostAsync("extract-ticket-pdf-info", content);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<PdfTicketDTO>(json)!;
    }
}