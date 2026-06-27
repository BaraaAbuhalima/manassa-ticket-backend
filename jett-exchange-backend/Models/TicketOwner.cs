using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jett_exchange_backend.Models;

public class TicketOwner
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey(nameof(User))]
    public required Guid UserId { get; set; }
    [ForeignKey(nameof(Ticket))]
    public required Guid TicketId { get; set; }

    public required User User { get; set; }
    public required Ticket Ticket { get; set; }

    public required PaymentMethod PaymentMethod { get; set; }

    public PaymentInfo PaymentInfo { get; set; }
    public required string PINHashed { get; set; }

}