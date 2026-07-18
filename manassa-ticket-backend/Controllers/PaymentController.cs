using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Services.Payments;
using Microsoft.AspNetCore.Mvc;

namespace manassa_ticket_backend.Controllers;

[ApiController]
[Route("api/payment")]
public class PaymentController(ITicketPurchaseService ticketPurchaseService) : ControllerBase
{
    [HttpPost("ticket/{id:guid}")]
    public async Task<IActionResult> PurchaseTicket(Guid id, [FromBody] PurchaseTicketRequest request)
    {
        var response = await ticketPurchaseService.CreatePaymentIntentAsync(id, request);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        Console.WriteLine("Stripe webhook received");
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();

        var response = await ticketPurchaseService.HandleStripeWebhookAsync(json, signature);
        return StatusCode(response.StatusCode, response);
    }
}
