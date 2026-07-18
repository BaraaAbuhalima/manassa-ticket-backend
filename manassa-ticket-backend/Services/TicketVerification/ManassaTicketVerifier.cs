using System.Text.Json;
using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.DTOs.TicketVerification;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.TicketVerification;

public class ManassaTicketVerifier : ITicketVerifier
{
    private readonly HttpClient _client;
    private readonly ILogger<ManassaTicketVerifier> _logger;

    public ManassaTicketVerifier(HttpClient httpClient, IOptions<ManassaApiOptions> options, ILogger<ManassaTicketVerifier> logger)
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