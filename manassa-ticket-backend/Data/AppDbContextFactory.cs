using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace manassa_ticket_backend.Data;

// Used only by `dotnet ef migrations add` to build AppDbContext at design time, so it knows
// to generate Postgres SQL syntax. `migrations add` just diffs the C# model against the prior
// snapshot — it never opens this connection, so the string below is a dummy that is never
// dialed. The real connection string (and the actual `Database.Migrate()` call that applies
// migrations to the live server) lives in Program.cs, driven by ConnectionStrings:Postgres.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused;Database=unused")
            .Options;

        return new AppDbContext(options);
    }
}