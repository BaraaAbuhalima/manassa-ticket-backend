using jett_exchange_backend.Models;

namespace jett_exchange_backend.Repositories;

public interface INormalJettTicketRepository
{
    Task<IEnumerable<NormalJettTicket>> GetAllAsync();
    Task<NormalJettTicket?> GetByIdAsync(int id);
    Task AddAsync(NormalJettTicket ticket);
    Task UpdateAsync(NormalJettTicket ticket);
    Task DeleteAsync(int id);
}
