using jett_exchange_backend.Models;

namespace jett_exchange_backend.Services;

public interface ITicketService
{
    Task<Ticket?> GetByIdAsync(Guid id);
}