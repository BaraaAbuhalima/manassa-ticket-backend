using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Models;

[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(254)]
    public required string Email { get; set; }
    [MaxLength(30)]
    public required string Phone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
