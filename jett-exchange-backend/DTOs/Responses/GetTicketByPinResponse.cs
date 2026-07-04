using jett_exchange_backend.Models;

namespace jett_exchange_backend.DTOs.Responses;

public class GetTicketByPinResponse
{
    public required Guid Id { get; set; }
    public required DateTime TicketDateTime { get; set; }
    public required int NumberOfBags { get; set; }
    public required decimal TotalPrice { get; set; }
    public required TicketSellStatus Status { get; set; }
    public DateTime? SoldAt { get; set; }
    public required string SellerEmail { get; set; }
    public required string SellerPhone { get; set; }
    public required PaymentMethod PaymentMethod { get; set; }
    public required PaymentInfo PaymentInfo { get; set; }
}
