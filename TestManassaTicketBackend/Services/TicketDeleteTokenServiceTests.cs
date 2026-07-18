using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TestManassaTicketBackend.TestHelpers;

namespace TestManassaTicketBackend.Services;

public class TicketDeleteTokenServiceTests
{
    [Test]
    public void GenerateToken_ThenValidate_ReturnsOriginalTicketId()
    {
        var sut = TestTicketDeleteTokenService.Create();
        var ticketId = Guid.NewGuid();

        var token = sut.GenerateToken(ticketId);
        var result = sut.ValidateAndGetTicketId(token);

        result.Should().Be(ticketId);
    }

    [Test]
    public void GenerateToken_ProducesDifferentTokens_ForDifferentTickets()
    {
        var sut = TestTicketDeleteTokenService.Create();

        var tokenA = sut.GenerateToken(Guid.NewGuid());
        var tokenB = sut.GenerateToken(Guid.NewGuid());

        tokenA.Should().NotBe(tokenB);
    }

    [Test]
    public void ValidateAndGetTicketId_ReturnsNull_ForMalformedToken()
    {
        var sut = TestTicketDeleteTokenService.Create();

        var result = sut.ValidateAndGetTicketId("not-a-jwt");

        result.Should().BeNull();
    }

    [Test]
    public void ValidateAndGetTicketId_ReturnsNull_ForEmptyToken()
    {
        var sut = TestTicketDeleteTokenService.Create();

        var result = sut.ValidateAndGetTicketId("");

        result.Should().BeNull();
    }

    [Test]
    public void ValidateAndGetTicketId_ReturnsNull_WhenTokenExpired()
    {
        var sut = TestTicketDeleteTokenService.Create(expirationMinutes: -1);
        var token = sut.GenerateToken(Guid.NewGuid());

        var result = sut.ValidateAndGetTicketId(token);

        result.Should().BeNull();
    }

    [Test]
    public void ValidateAndGetTicketId_ReturnsNull_WhenSignedWithDifferentKey()
    {
        var validator = TestTicketDeleteTokenService.Create();
        // Simulate a token forged/signed by a service configured with a different key.
        var differentKeyService = new TicketDeleteTokenService(Options.Create(new JwtOptions
        {
            SigningKey = "a-completely-different-signing-key-that-is-also-long-enough",
            TicketDeleteTokenExpirationMinutes = 15
        }));

        var token = differentKeyService.GenerateToken(Guid.NewGuid());
        var result = validator.ValidateAndGetTicketId(token);

        result.Should().BeNull();
    }

    [Test]
    public void ValidateAndGetTicketId_ReturnsNull_WhenSignatureTampered()
    {
        var sut = TestTicketDeleteTokenService.Create();
        var token = sut.GenerateToken(Guid.NewGuid());
        var segments = token.Split('.');
        var tampered = $"{segments[0]}.{segments[1]}.{new string('x', segments[2].Length)}";

        var result = sut.ValidateAndGetTicketId(tampered);

        result.Should().BeNull();
    }
}
