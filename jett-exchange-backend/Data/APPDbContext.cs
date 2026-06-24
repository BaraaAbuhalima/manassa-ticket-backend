using Microsoft.EntityFrameworkCore;
using jett_exchange_backend.Models;

namespace jett_exchange_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<NormalJettTicket> Tickets { get; set; }
}