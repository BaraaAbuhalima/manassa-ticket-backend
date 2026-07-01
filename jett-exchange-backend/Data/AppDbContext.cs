using System.Text.Json;
using jett_exchange_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketDateSubscription> TicketDateSubscriptions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ticket>()
            .Property(t => t.PaymentInfo)
            .HasConversion(
                paymentInfo => JsonSerializer.Serialize(paymentInfo, PaymentInfo.JsonOptions),
                json => JsonSerializer.Deserialize<PaymentInfo>(json, PaymentInfo.JsonOptions)!);
    }
}