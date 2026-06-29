using System.Text.Json;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.DTOs;
using Microsoft.Extensions.Options;

namespace jett_exchange_backend.Services;

public class TicketDataExtractor : ITicketDataExtractor
{
    private readonly HttpClient _client;

    public TicketDataExtractor(
        HttpClient httpClient,
        IOptions<PythonExtractorService> options)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri(options.Value.BaseUrl);
    }

    public async Task<RawTicketDTO> ExtractTicketAsync(string filePath)
    {
        var response = await _client.GetAsync(
            $"extract-ticket-pdf-info?file_path={filePath}");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<RawTicketDTO>(json)!;
    }
}