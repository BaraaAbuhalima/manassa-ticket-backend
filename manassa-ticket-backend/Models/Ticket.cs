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
    [MaxLength(30)]
    public string? TicketId { get; set; }
    [MaxLength(50)]
    public string? OriginalOwnerName { get; set; }
    [MaxLength(20)]
    public string? OriginalOwnerPassportNumber { get; set; }

    public DateTime? TicketDateTime { get; set; }


    public int? NumberOfBags { get; set; }
    // The seller's asking price in JOD - set at listing time, editable via ModifyTicketAsync.
    // USD equivalents are derived on the fly (CurrencyConversion.JodToUsd) rather than stored,
    // since the conversion rate is a fixed constant, not a live rate.
    [Column(TypeName = "decimal(10,2)")]
    public required decimal SellerAskedPriceJod { get; set; }
    // In JOD, matching the currency printed on the original ticket — not a USD amount.
    // Mixing the two here is exactly the bug that motivated this comment.
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
    // The amount in USD actually captured from the buyer (listed price + buyer service fee),
    // taken directly from Stripe's PaymentIntent.Amount at capture time. Fixed at sale, unlike
    // SellerAskedPriceJod (the seller can edit it pre-sale) and the fee config (which can change
    // over time) - both of which SoldAtPriceUsd would otherwise silently drift from if derived.
    [Column(TypeName = "decimal(10,2)")]
    public decimal? SoldAtPriceUsd { get; set; }
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
