using jett_exchange_backend.DTOs.TicketVerification;

namespace jett_exchange_backend.Services.TicketVerification;

public interface ITicketVerifier
{
    Task<VerifiedTicketDTO> VerifyTicketAsync(string ticketId);
}