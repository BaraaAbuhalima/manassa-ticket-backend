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
    // How long a buyer gets to complete checkout before the hold is released back to
    // ForSale. Shared with TicketReservationSweeper, which does the actual release.
    public static readonly TimeSpan ReservationTtl = TimeSpan.FromMinutes(15);

    public async Task<ApiResponse<PurchaseTicketResponse>> CreatePaymentIntentAsync(Guid ticketId, PurchaseTicketRequest request)
    {
        var reservedAt = DateTime.UtcNow;
        var reservationCutoff = reservedAt - ReservationTtl;

        // Claim the ticket with a single conditional UPDATE instead of a read-then-write:
        // the eligibility check (ForSale, or a Reserved hold that's expired) and the flip
        // to Reserved happen as one statement, so Postgres serializes concurrent claims on
        // the same row. Only one of two simultaneous buyers can ever affect a row here;
        // the other gets 0 rows affected and knows someone beat them to it. This closes the
        // race where both could pass an "is it ForSale?" check and each get a PaymentIntent
        // for the same ticket.
        var claimed = await dbContext.Tickets
            .Where(t => t.Id == ticketId &&
                (t.Status == TicketSellStatus.ForSale ||
                 (t.Status == TicketSellStatus.Reserved && t.ReservedAt < reservationCutoff)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Status, TicketSellStatus.Reserved)
                .SetProperty(t => t.ReservedAt, reservedAt)
                .SetProperty(t => t.BuyerName, request.BuyerName)
                .SetProperty(t => t.BuyerEmail, request.BuyerEmail));

        if (claimed == 0)
        {
            var exists = await dbContext.Tickets.AnyAsync(t => t.Id == ticketId);
            if (!exists)
            {
                return TicketResponses.NotFound<PurchaseTicketResponse>();
            }

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

        var ticket = await dbContext.Tickets.FirstAsync(t => t.Id == ticketId);
        var currency = stripeOptions.Value.Currency;
        var amountInSmallestUnit = (long)Math.Round(ticket.TotalPriceUsd * 100, MidpointRounding.AwayFromZero);

        PaymentIntent paymentIntent;
        try
        {
            var paymentIntentService = new PaymentIntentService();
            paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
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
        }
        catch
        {
            // Give the hold back immediately rather than making the ticket unavailable
            // for the full TTL just because Stripe rejected the request.
            await dbContext.Tickets
                .Where(t => t.Id == ticketId && t.Status == TicketSellStatus.Reserved && t.ReservedAt == reservedAt)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Status, TicketSellStatus.ForSale)
                    .SetProperty(t => t.ReservedAt, (DateTime?)null)
                    .SetProperty(t => t.BuyerName, (string?)null)
                    .SetProperty(t => t.BuyerEmail, (string?)null));
            throw;
        }

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
            else if (ticket.Status == TicketSellStatus.Sold)
            {
                // Stripe webhooks are at-least-once delivery; a redelivered
                // payment_intent.succeeded for an already-sold ticket is expected, not an error.
                logger.LogInformation(
                    "Ignoring duplicate payment_intent.succeeded for already-sold ticket {TicketId}", ticket.Id);
            }
            else if (ticket.Status != TicketSellStatus.Reserved)
            {
                // The reservation must have expired and been released (or the ticket was
                // otherwise deleted/reposted) before this payment completed.
                logger.LogWarning(
                    "Payment succeeded for ticket {TicketId} but it was {Status}, not Reserved; not marking as sold",
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
            var date = ticket.TicketDateTime!.Value.ToString("yyyy-MM-dd");

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