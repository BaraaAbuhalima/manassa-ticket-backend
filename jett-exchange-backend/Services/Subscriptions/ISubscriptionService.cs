using jett_exchange_backend.Common;
using jett_exchange_backend.DTOs.Requests;

namespace jett_exchange_backend.Services.Subscriptions;

public interface ISubscriptionService
{
    Task<ApiResponse<string>> SubscribeAsync(SubscribeRequest request);
}
