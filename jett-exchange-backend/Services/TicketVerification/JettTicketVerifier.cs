using System.Text.Json;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.DTOs.TicketVerification;
using Microsoft.Extensions.Options;

namespace jett_exchange_backend.Services.TicketVerification;

public class JettTicketVerifier : ITicketVerifier
{
    private readonly HttpClient _client;
    private readonly ILogger<JettTicketVerifier> _logger;

    public JettTicketVerifier(HttpClient httpClient, IOptions<JettApiOptions> options, ILogger<JettTicketVerifier> logger)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri(options.Value.BaseUrl);
        _logger = logger;
    }

    public async Task<VerifiedTicketDTO> VerifyTicketAsync(string barcode)
    {
        try
        {
            var response = await _client.PostAsJsonAsync(
                "booking/ticket-details-by-barcode",
                new
                {
                    barcode,
                    lang = "en"
                });

            response.EnsureSuccessStatusCode();

            var ticketDetails = await response.Content.ReadFromJsonAsync<TransferTicketDTO>();
            return ticketDetails is null
                ? new VerifiedTicketDTO { Success = false }
                : new VerifiedTicketDTO(ticketDetails);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or TaskCanceledException)
        {
            _logger.LogError(ex, "Failed to verify ticket for barcode {Barcode}", barcode);
            return new VerifiedTicketDTO { Success = false };
        }
    }
}