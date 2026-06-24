using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

}