using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using manassa_ticket_backend.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace manassa_ticket_backend.Services.Tickets;

public class TicketDeleteTokenService(IOptions<JwtOptions> options) : ITicketDeleteTokenService
{
    private const string TicketIdClaimType = "ticketId";

    public string GenerateToken(Guid ticketId)
    {
        var credentials = new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: [new Claim(TicketIdClaimType, ticketId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(options.Value.TicketDeleteTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? ValidateAndGetTicketId(string token)
    {
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SigningKey(),
                ClockSkew = TimeSpan.Zero
            }, out _);

            var claim = principal.FindFirst(TicketIdClaimType);
            return claim is not null && Guid.TryParse(claim.Value, out var ticketId) ? ticketId : null;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    private SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(options.Value.SigningKey));
}
