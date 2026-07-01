using jett_exchange_backend.Common;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.DTOs.Responses;

namespace jett_exchange_backend.Services.Payments;

public interface ITicketPurchaseService
{
    Task<ApiResponse<PurchaseTicketResponse>> CreatePaymentIntentAsync(Guid ticketId, PurchaseTicketRequest request);
    Task<ApiResponse<string>> HandleStripeWebhookAsync(string json, string stripeSignatureHeader);
}
