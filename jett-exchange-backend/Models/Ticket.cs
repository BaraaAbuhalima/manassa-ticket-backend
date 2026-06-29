using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Models;


[Index(nameof(TicketId), IsUnique = true)]
public class Ticket
{

    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(30)]
    public required string TicketId { get; set; }
    [MaxLength(50)]
    public required string OriginalOwnerName { get; set; }
    [MaxLength(20)]
    public required string OriginalOwnerPassportNumber { get; set; }

    public DateTime TicketDateTime { get; set; }

    [Column(TypeName = "decimal(3,2)")]
    public required decimal Price { get; set; }
    public required int NumberOfBags { get; set; }
    public required decimal TotalPrice { get; set; }
    [MaxLength(100)]
    public required string SellerName { get; set; }

    [MaxLength(254)]
    public required string SellerEmail { get; set; }
    [MaxLength(30)]
    public required string SellerPhone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public required PaymentMethod PaymentMethod { get; set; }

    public required PaymentInfo PaymentInfo { get; set; }
    public required string PinHashed { get; set; }
    public required TicketSellStatus status { get; set; }
    public required string TicketFilePath { get; set; }


}
