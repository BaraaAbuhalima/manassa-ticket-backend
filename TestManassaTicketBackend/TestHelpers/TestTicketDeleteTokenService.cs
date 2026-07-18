using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Services.Tickets;
using Microsoft.Extensions.Options;

namespace TestManassaTicketBackend.TestHelpers;

public static class TestTicketDeleteTokenService
{
    public const string SigningKey = "test-signing-key-at-least-32-bytes-long-for-hmac-sha256!!";

    public static TicketDeleteTokenService Create(int expirationMinutes = 15)
    {
        var options = Options.Create(new JwtOptions
        {
            SigningKey = SigningKey,
            TicketDeleteTokenExpirationMinutes = expirationMinutes
        });

        return new TicketDeleteTokenService(options);
    }
}
