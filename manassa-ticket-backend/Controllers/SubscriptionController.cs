using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Services.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace manassa_ticket_backend.Controllers;

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
