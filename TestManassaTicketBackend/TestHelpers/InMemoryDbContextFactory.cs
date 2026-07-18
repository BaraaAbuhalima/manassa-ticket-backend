using manassa_ticket_backend.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TestManassaTicketBackend.TestHelpers;

public static class InMemoryDbContextFactory
{
    public static AppDbContext Create(string? databaseName = null)
    {
        var connection = new SqliteConnection(
            $"DataSource=file:{databaseName ?? Guid.NewGuid().ToString()}?mode=memory&cache=shared");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }
}
