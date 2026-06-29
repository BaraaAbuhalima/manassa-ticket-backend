using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Models;

namespace jett_exchange_backend.Services;

public interface ITicketService
{
    Task<Ticket?> GetByIdAsync(Guid id);
    Task<bool> DeleteByIdAsync(Guid id);
    Task<bool> DeleteByRefAsync(string Ref);
    Task<bool> PostTicketAsync(PostTicketRequest request);
}