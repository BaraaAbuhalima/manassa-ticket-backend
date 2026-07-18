using manassa_ticket_backend.Models;

namespace manassa_ticket_backend.DTOs.Requests;

public class PostTicketRequest
{
    public required string FileKey { get; set; }
    public required string SellerEmail { get; set; }
    public required string SellerPhone { get; set; }
    public decimal Price { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public required PaymentInfoRequest PaymentInfoRequest { get; set; }
    public required string SellerName { get; set; }
}