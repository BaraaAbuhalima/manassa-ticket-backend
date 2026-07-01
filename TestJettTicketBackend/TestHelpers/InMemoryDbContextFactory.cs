using jett_exchange_backend.Data;
using Microsoft.EntityFrameworkCore;

namespace TestJettTicketBackend.TestHelpers;

public static class InMemoryDbContextFactory
{
    public static AppDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}