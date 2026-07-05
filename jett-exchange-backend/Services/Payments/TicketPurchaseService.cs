using jett_exchange_backend.Common;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.DTOs.Responses;
using jett_exchange_backend.Messaging;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace jett_exchange_backend.Services.Payments;

public class TicketPurchaseService(
    AppDbContext dbContext,
    IOptions<StripeOptions> stripeOptions,
    ITicketSoldNotificationPublisher soldNotificationPublisher,
    ITicketPurchasedNotificationPublisher purchasedNotificationPublisher,
    ILogger<TicketPurchaseService> logger)
    : ITicketPurchaseService
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
                Errors = ["Ticket is not available for purchase"],
                Links = new Dictionary<string, string>
                {
                    { "home", "/home" },
                }
            };
        }

        var currency = stripeOptions.Value.Currency;
        var amountInSmallestUnit = (long)Math.Round(ticket.TotalPriceUsd * 100, MidpointRounding.AwayFromZero);

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
                Amount = ticket.TotalPriceUsd,
                Currency = currency
            },
            Links = new Dictionary<string, string>
            {
                { "home", "/home" },
                { "ticket", "/ticket?id=" + ticket.Id },
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
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Rejected Stripe webhook: signature verification failed");
            return new ApiResponse<string>
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Success = false,
                Message = "Invalid Stripe webhook signature",
                Errors = ["Invalid Stripe webhook signature"]
            };
        }
        Console.WriteLine(stripeEvent);
        if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
        {
            var ticket = await dbContext.Tickets
                .FirstOrDefaultAsync(t => t.StripePaymentIntentId == paymentIntent.Id);
            Console.WriteLine(ticket);
            if (ticket is null)
            {
                logger.LogWarning(
                    "Received Stripe event {EventType} for payment intent {PaymentIntentId} but no ticket references it",
                    stripeEvent.Type, paymentIntent.Id);
            }
            else if (stripeEvent.Type != "payment_intent.succeeded")
            {
                logger.LogInformation(
                    "Ignoring Stripe event {EventType} for ticket {TicketId}", stripeEvent.Type, ticket.Id);
            }
            else if (ticket.Status != TicketSellStatus.ForSale)
            {
                logger.LogWarning(
                    "Payment succeeded for ticket {TicketId} but it was already {Status}; not marking as sold",
                    ticket.Id, ticket.Status);
            }
            else
            {
                ticket.Status = TicketSellStatus.Sold;
                ticket.SoldAt = DateTime.UtcNow;
                dbContext.Tickets.Update(ticket);
                await dbContext.SaveChangesAsync();
                logger.LogInformation(
                    "Marked ticket {TicketId} as sold from payment intent {PaymentIntentId}", ticket.Id, paymentIntent.Id);

                await PublishSoldNotificationsAsync(ticket);
            }
        }
        else
        {
            logger.LogInformation("Ignoring Stripe event {EventType}: not a payment intent", stripeEvent.Type);
        }

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Webhook processed successfully"
        };
    }

    private async Task PublishSoldNotificationsAsync(Ticket ticket)
    {
        try
        {
            var date = ticket.TicketDateTime.ToString("yyyy-MM-dd");

            await soldNotificationPublisher.PublishAsync(new SendEmailMessage
            {
                To = ticket.SellerEmail,
                Subject = "Your ticket has sold",
                Body = $"Good news — your ticket for {date} has sold on Jett Ticket Exchange."
            });

            if (ticket.BuyerEmail is null)
            {
                logger.LogWarning("Ticket {TicketId} was sold but has no BuyerEmail; skipping buyer notification", ticket.Id);
                return;
            }

            await purchasedNotificationPublisher.PublishAsync(new SendEmailMessage
            {
                To = ticket.BuyerEmail,
                Subject = "Your Jett Ticket Exchange purchase",
                Body = $"Thanks for your purchase! Your ticket for {date} is attached.",
                AttachmentPath = ticket.TicketFilePath,
                AttachmentFileName = $"ticket-{ticket.Pin}.pdf"
            });
        }
        catch (Exception ex)
        {
            // A broker outage shouldn't fail webhook processing; the sale is already
            // recorded, the seller/buyer just won't get an email for it.
            logger.LogError(ex, "Failed to publish sold-notification emails for ticket {TicketId}", ticket.Id);
        }
    }
}
