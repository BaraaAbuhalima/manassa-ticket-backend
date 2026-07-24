using manassa_ticket_backend.Models;

namespace manassa_ticket_backend.DTOs.Responses;

public class GetTicketByPinResponse
{
    public required Guid Id { get; set; }
    public DateTime? TicketDateTime { get; set; }
    public int? NumberOfBags { get; set; }
    public required decimal SellerAskedPriceJod { get; set; }
    public required TicketSellStatus Status { get; set; }
    public DateTime? SoldAt { get; set; }
    public required string SellerEmail { get; set; }
    public required string SellerPhone { get; set; }
    public required PaymentMethod PaymentMethod { get; set; }
    public required PaymentInfo PaymentInfo { get; set; }
}
