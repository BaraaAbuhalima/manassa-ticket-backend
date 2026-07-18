using manassa_ticket_backend.DTOs.TicketVerification;

namespace manassa_ticket_backend.Services.TicketVerification;

public interface ITicketVerifier
{
    Task<VerifiedTicketDTO> VerifyTicketAsync(string ticketId);
}