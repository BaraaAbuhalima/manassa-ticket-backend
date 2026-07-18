using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using manassa_ticket_backend.Common;
using Microsoft.EntityFrameworkCore;

namespace manassa_ticket_backend.Models;


[Index(nameof(TicketId), IsUnique = true)]

[Index(nameof(Pin), IsUnique = true)]
public class Ticket
{

    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    // Null until the background job extracts/verifies the PDF (Status == Processing until then).
    [MaxLength(30)]
    public string? TicketId { get; set; }
    [MaxLength(50)]
    public string? OriginalOwnerName { get; set; }
    [MaxLength(20)]
    public string? OriginalOwnerPassportNumber { get; set; }

    public DateTime? TicketDateTime { get; set; }


    public int? NumberOfBags { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public required decimal TotalPriceUsd { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public required decimal TotalPriceJod { get; set; }
    // In JOD, matching the currency printed on the original ticket — not TotalPriceUsd's
    // currency. Mixing the two here is exactly the bug that motivated this comment.
    [Column(TypeName = "decimal(10,2)")]
    public decimal? OriginalPrice { get; set; }
    [MaxLength(100)]
    public required string SellerName { get; set; }

    [MaxLength(ValidationConstants.EmailMaxLength)]
    public required string SellerEmail { get; set; }
    [MaxLength(ValidationConstants.PhoneMaxLength)]
    public required string SellerPhone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public required PaymentMethod PaymentMethod { get; set; }

    public required PaymentInfo PaymentInfo { get; set; }
    [MaxLength(30)]
    public required string Pin { get; set; }
    public required TicketSellStatus Status { get; set; }
    public DateTime? SoldAt { get; set; }
    public DateTime? ReservedAt { get; set; }
    [MaxLength(100)]
    public required string TicketFilePath { get; set; }
    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    [MaxLength(50)]
    public string? BuyerName { get; set; }
    [MaxLength(ValidationConstants.EmailMaxLength)]
    public string? BuyerEmail { get; set; }
    [MaxLength(255)]
    public string? StripePaymentIntentId { get; set; }
}
