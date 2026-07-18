using manassa_ticket_backend.Common;
using manassa_ticket_backend.DTOs.Requests;

namespace manassa_ticket_backend.Services.Subscriptions;

public interface ISubscriptionService
{
    Task<ApiResponse<string>> SubscribeAsync(SubscribeRequest request);
}
