using System.Text.Json;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.DTOs.TicketExtraction;
using Microsoft.Extensions.Options;

namespace jett_exchange_backend.Services.TicketExtraction;

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

    public async Task<PdfTicketDTO> ExtractTicketAsync(string filePath)
    {
        var encodedPath = Uri.EscapeDataString(filePath);

        var response = await _client.PostAsync(
            $"extract-ticket-pdf-info?file_path={encodedPath}",
            content: null);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<PdfTicketDTO>(json)!;
    }
}