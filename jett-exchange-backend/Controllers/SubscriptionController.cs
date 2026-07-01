using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Services.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace jett_exchange_backend.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionController(ISubscriptionService subscriptionService) : ControllerBase
{
    [HttpPost("")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        var response = await subscriptionService.SubscribeAsync(request);
        return StatusCode(response.StatusCode, response);
    }
}
