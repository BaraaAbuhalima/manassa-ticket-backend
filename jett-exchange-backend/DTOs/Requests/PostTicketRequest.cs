using jett_exchange_backend.Models;

namespace jett_exchange_backend.DTOs.Requests;

public class PostTicketRequest
{
    public IFormFile File { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public decimal Price { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentInfoRequest PaymentInfoRequest { get; set; }
}