using System.ComponentModel.DataAnnotations;
using jett_exchange_backend.Common;

namespace jett_exchange_backend.DTOs.Requests;

public class PurchaseTicketRequest
{
    [MaxLength(50)]
    public required string BuyerName { get; set; }
    [MaxLength(ValidationConstants.EmailMaxLength)]
    public required string BuyerEmail { get; set; }
}
