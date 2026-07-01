namespace jett_exchange_backend.Configuration;

public class JwtOptions
{
    public string SigningKey { get; set; } = string.Empty;
    public int TicketDeleteTokenExpirationMinutes { get; set; } = 15;
}
