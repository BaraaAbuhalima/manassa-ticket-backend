using System.Text.Json;
using manassa_ticket_backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace manassa_ticket_backend.Data;

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

        // Stored by name rather than the default int, so appending new members (or, unlike
        // the int encoding, even reordering existing ones) can never silently reinterpret a
        // row already sitting in the database.
        modelBuilder.Entity<Ticket>()
            .Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<Ticket>()
            .Property(t => t.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Postgres's timestamptz rejects DateTime.Kind=Local outright (and treats Unspecified
        // as an app-level footgun). Force every stored DateTime through Utc here so a Kind
        // slipping in from an external API or JSON-bound request can't crash SaveChanges.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => NormalizeToUtc(v),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? NormalizeToUtc(v.Value) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                // TicketDateTime is the bus's scheduled travel date/time as extracted from the
                // ticket, not a real-world instant — store it exactly as extracted rather than
                // forcing it through Utc (handled separately below via a tz-less column type).
                if (entityType.ClrType == typeof(Ticket) && property.Name == nameof(Ticket.TicketDateTime))
                {
                    continue;
                }

                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
            }
        }

        // "timestamp without time zone" has no Kind requirement, so the extracted travel
        // date/time is written and read back byte-for-byte, with no timezone reinterpretation
        // by Postgres or by tools (e.g. Supabase Studio, psql) that display timestamptz columns
        // converted to the viewer's local timezone.
        modelBuilder.Entity<Ticket>()
            .Property(t => t.TicketDateTime)
            .HasColumnType("timestamp without time zone");
    }

    private static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}