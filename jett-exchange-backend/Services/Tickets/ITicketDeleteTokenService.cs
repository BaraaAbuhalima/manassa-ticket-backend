namespace jett_exchange_backend.Services.Tickets;

public interface ITicketDeleteTokenService
{
    string GenerateToken(Guid ticketId);
    Guid? ValidateAndGetTicketId(string token);
}
