using System.ComponentModel.DataAnnotations;
using manassa_ticket_backend.Common;

namespace manassa_ticket_backend.DTOs.Requests;

public class PurchaseTicketRequest
{
    [MaxLength(50)]
    public required string BuyerName { get; set; }
    [MaxLength(ValidationConstants.EmailMaxLength)]
    public required string BuyerEmail { get; set; }
}
