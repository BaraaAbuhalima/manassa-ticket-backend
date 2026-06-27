using jett_exchange_backend.Data;
using jett_exchange_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Services;

public class TicketService : ITicketService
{
    private readonly AppDbContext _dbContext;
    public TicketService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<Ticket?> GetByIdAsync(Guid id)
    {
        return await _dbContext.NormalJettTickets
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        var ticket = await _dbContext.NormalJettTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return false;
        }

        _dbContext.NormalJettTickets.Remove(ticket);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}