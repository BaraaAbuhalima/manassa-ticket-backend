using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Models;

[Index(nameof(Email), nameof(Date), IsUnique = true)]
public class TicketDateSubscription
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(254)]
    public required string Email { get; set; }
    public required DateOnly Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Notified { get; set; }
}
