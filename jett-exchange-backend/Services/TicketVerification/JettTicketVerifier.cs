using jett_exchange_backend.Configuration;
using jett_exchange_backend.DTOs.TicketVerification;
using Microsoft.Extensions.Options;

namespace jett_exchange_backend.Services.TicketVerification;

public class JettTicketVerifier : ITicketVerifier
{
    private readonly HttpClient _client;

    public JettTicketVerifier(HttpClient httpClient, IOptions<JettApiOptions> options)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri(options.Value.BaseUrl);
    }

    public async Task<VerifiedTicketDTO> VerifyTicketAsync(string barcode)
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
        if (ticketDetails is null)
        {
            return new VerifiedTicketDTO
            {
                Success = false
            };
        }

        return new VerifiedTicketDTO(ticketDetails);
    }
}