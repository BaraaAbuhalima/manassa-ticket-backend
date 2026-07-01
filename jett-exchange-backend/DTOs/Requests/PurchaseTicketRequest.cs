using System.ComponentModel.DataAnnotations;

namespace jett_exchange_backend.DTOs.Requests;

public class PurchaseTicketRequest
{
    [MaxLength(50)]
    public required string BuyerName { get; set; }
    [MaxLength(254)]
    public required string BuyerEmail { get; set; }
}
