using Microsoft.EntityFrameworkCore;
using jett_exchange_backend.Models;

namespace jett_exchange_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<TicketOwner> TicketOwners { get; set; }
    public DbSet<PaymentInfo> PaymentInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BankTransferInfo>().HasBaseType<PaymentInfo>();
        modelBuilder.Entity<PhoneTransfer>().HasBaseType<PaymentInfo>();
        modelBuilder.Entity<Reflect>().HasBaseType<PaymentInfo>();

        modelBuilder.Entity<PaymentInfo>()
            .HasOne(p => p.TicketOwner)
            .WithOne(to => to.PaymentInfo)
            .HasForeignKey<PaymentInfo>(p => p.TicketOwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}