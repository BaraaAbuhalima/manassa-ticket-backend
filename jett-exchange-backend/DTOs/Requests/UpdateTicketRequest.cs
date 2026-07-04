using jett_exchange_backend.Models;

namespace jett_exchange_backend.DTOs.Requests;

public class UpdateTicketRequest
{
    public PaymentUpdateRequest? Payment { get; set; }
    public decimal? Price { get; set; }
}

public class PaymentUpdateRequest
{
    public required PaymentMethod PaymentMethod { get; set; }
    public required PaymentInfoRequest PaymentInfoRequest { get; set; }
}