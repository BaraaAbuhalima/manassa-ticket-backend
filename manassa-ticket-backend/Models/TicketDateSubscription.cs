using System.ComponentModel.DataAnnotations;
using manassa_ticket_backend.Common;
using Microsoft.EntityFrameworkCore;

namespace manassa_ticket_backend.Models;

[Index(nameof(Email), nameof(Date), IsUnique = true)]
public class TicketDateSubscription
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(ValidationConstants.EmailMaxLength)]
    public required string Email { get; set; }
    public required DateOnly Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Notified { get; set; }
}
