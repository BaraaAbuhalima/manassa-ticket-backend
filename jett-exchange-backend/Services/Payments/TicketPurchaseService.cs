using jett_exchange_backend.Common;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.DTOs.Responses;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace jett_exchange_backend.Services.Payments;

public class TicketPurchaseService(AppDbContext dbContext, IOptions<StripeOptions> stripeOptions) : ITicketPurchaseService
{
    public async Task<ApiResponse<PurchaseTicketResponse>> CreatePaymentIntentAsync(Guid ticketId, PurchaseTicketRequest request)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null)
        {
            return TicketResponses.NotFound<PurchaseTicketResponse>();
        }

        if (ticket.Status != TicketSellStatus.ForSale)
        {
            return new ApiResponse<PurchaseTicketResponse>
            {
                StatusCode = StatusCodes.Status409Conflict,
                Success = false,
                Message = "Ticket is not available for purchase",
                Errors = ["Ticket is not available for purchase"]
            };
        }

        var currency = stripeOptions.Value.Currency;
        var amountInSmallestUnit = (long)Math.Round(ticket.TotalPrice * 100, MidpointRounding.AwayFromZero);

        var paymentIntentService = new PaymentIntentService();
        var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amountInSmallestUnit,
            Currency = currency,
            ReceiptEmail = request.BuyerEmail,
            Metadata = new Dictionary<string, string>
            {
                { "ticketId", ticket.Id.ToString() },
                { "buyerEmail", request.BuyerEmail }
            },
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            }
        });

        ticket.BuyerName = request.BuyerName;
        ticket.BuyerEmail = request.BuyerEmail;
        ticket.StripePaymentIntentId = paymentIntent.Id;
        dbContext.Tickets.Update(ticket);
        await dbContext.SaveChangesAsync();

        return new ApiResponse<PurchaseTicketResponse>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Payment intent created successfully",
            Data = new PurchaseTicketResponse
            {
                TicketId = ticket.Id,
                ClientSecret = paymentIntent.ClientSecret,
                PublishableKey = stripeOptions.Value.PublishableKey,
                Amount = ticket.TotalPrice,
                Currency = currency
            }
        };
    }

    public async Task<ApiResponse<string>> HandleStripeWebhookAsync(string json, string stripeSignatureHeader)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignatureHeader, stripeOptions.Value.WebhookSecret);
        }
        catch (StripeException)
        {
            return new ApiResponse<string>
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Success = false,
                Message = "Invalid Stripe webhook signature",
                Errors = ["Invalid Stripe webhook signature"]
            };
        }

        if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
        {
            var ticket = await dbContext.Tickets
                .FirstOrDefaultAsync(t => t.StripePaymentIntentId == paymentIntent.Id);

            if (ticket is not null && stripeEvent.Type == "payment_intent.succeeded" && ticket.Status == TicketSellStatus.ForSale)
            {
                ticket.Status = TicketSellStatus.Sold;
                dbContext.Tickets.Update(ticket);
                await dbContext.SaveChangesAsync();
            }
        }

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Webhook processed successfully"
        };
    }
}
