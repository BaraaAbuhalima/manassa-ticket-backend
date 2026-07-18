using manassa_ticket_backend.Common;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.DTOs.Responses;

namespace manassa_ticket_backend.Services.Payments;

public interface ITicketPurchaseService
{
    Task<ApiResponse<PurchaseTicketResponse>> CreatePaymentIntentAsync(Guid ticketId, PurchaseTicketRequest request);
    Task<ApiResponse<string>> HandleStripeWebhookAsync(string json, string stripeSignatureHeader);
}
