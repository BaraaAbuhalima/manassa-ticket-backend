using manassa_ticket_backend.Common;
using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.DTOs.Responses;
using manassa_ticket_backend.Messaging;
using manassa_ticket_backend.Models;
using manassa_ticket_backend.Services.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace manassa_ticket_backend.Services.Payments;

public class TicketPurchaseService(
    AppDbContext dbContext,
    IOptions<StripeOptions> stripeOptions,
    IOptions<FeeOptions> feeOptions,
    ITicketSoldNotificationPublisher soldNotificationPublisher,
    ITicketPurchasedNotificationPublisher purchasedNotificationPublisher,
    ILogger<TicketPurchaseService> logger)
    : ITicketPurchaseService
{
    // No buyer waits on this in the normal path anymore: a ticket only becomes Reserved once
    // Stripe has already confirmed a card is good and we're seconds from capturing it (see
    // ClaimAndCaptureAsync). This TTL exists purely as a crash-recovery backstop — the window
    // where a ticket could get stuck Reserved is "the process died between the claim and the
    // capture call" — so it's kept short rather than sized for buyer patience.
    public static readonly TimeSpan ReservationTtl = TimeSpan.FromMinutes(2);

    public async Task<ApiResponse<PurchaseTicketResponse>> CreatePaymentIntentAsync(Guid ticketId, PurchaseTicketRequest request)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null)
        {
            return TicketResponses.NotFound<PurchaseTicketResponse>();
        }

        // Advisory only, not a lock: nothing is reserved by starting checkout, so this can go
        // stale the instant it's read. It just spares a buyer a full card-entry flow for a
        // ticket that's already definitely unavailable. The eligibility rule mirrors
        // ClaimAndCaptureAsync's — ForSale, or a Reserved hold stuck past its TTL — so this
        // check and the real one downstream never disagree about what's fair game.
        var reservationCutoff = DateTime.UtcNow - ReservationTtl;
        var possiblyAvailable = ticket.Status == TicketSellStatus.ForSale ||
            (ticket.Status == TicketSellStatus.Reserved && ticket.ReservedAt < reservationCutoff);

        if (!possiblyAvailable)
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

        // Buyer service fee: a flat amount plus a percentage of the listed price, both
        // configurable (Fee:FlatFeeUsd / Fee:PercentFee) and validated non-negative at
        // startup. The seller is paid the full listed price separately (payout here is
        // manual/off-platform — see PaymentInfo); this fee is what the platform keeps on
        // top, not a cut of what the seller receives.
        var baseUsd = CurrencyConversion.JodToUsd(ticket.SellerAskedPriceJod);
        var feeUsd = FeeCalculator.CalculateFeeUsd(
            baseUsd, feeOptions.Value.FlatFeeUsd, feeOptions.Value.PercentFee);
        var totalUsd = baseUsd + feeUsd;
        var amountInSmallestUnit = (long)Math.Round(totalUsd * 100, MidpointRounding.AwayFromZero);

        // No DB write here — nobody is blocked from starting checkout just because someone
        // else also started checkout on the same ticket. Every eligible buyer gets a
        // PaymentIntent; who actually wins the ticket is decided in ClaimAndCaptureAsync, at
        // the moment Stripe confirms a card is actually good for the money. Everyone else's
        // authorization gets canceled, never charged.
        var paymentIntentService = new PaymentIntentService();
        var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amountInSmallestUnit,
            Currency = currency,
            ReceiptEmail = request.BuyerEmail,
            // Authorize the card (confirms funds are available and places a hold) without
            // taking the money yet. The ticket id and buyer details travel in metadata rather
            // than being written to the ticket row, since we don't yet know if this buyer
            // will be the one who wins it.
            CaptureMethod = "manual",
            Metadata = new Dictionary<string, string>
            {
                { "ticketId", ticket.Id.ToString() },
                { "buyerName", request.BuyerName },
                { "buyerEmail", request.BuyerEmail },
                { "ticketPriceUsd", baseUsd.ToString("0.00") },
                { "feeUsd", feeUsd.ToString("0.00") }
            },
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            }
        });

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
                Amount = totalUsd,
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

        if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
        {
            if (stripeEvent.Type == "payment_intent.amount_capturable_updated")
            {
                // No ticket references this PaymentIntent yet — that's exactly what this event
                // is for. The ticket id travels in metadata instead of the ticket row.
                await ClaimAndCaptureAsync(paymentIntent);
            }
            else
            {
                var ticket = await dbContext.Tickets
                    .FirstOrDefaultAsync(t => t.StripePaymentIntentId == paymentIntent.Id);
                if (ticket is null)
                {
                    // Expected, not an error, for a losing authorization: it never won the claim,
                    // so no ticket ever pointed at it, and its own payment_intent.canceled/
                    // payment_failed events land here with nothing to do.
                    logger.LogInformation(
                        "Received Stripe event {EventType} for payment intent {PaymentIntentId} with no claimed ticket",
                        stripeEvent.Type, paymentIntent.Id);
                }
                else
                {
                    switch (stripeEvent.Type)
                    {
                        case "payment_intent.succeeded":
                            await MarkTicketSoldAsync(ticket, paymentIntent);
                            break;

                        case "payment_intent.canceled":
                            // The winning claim's own PaymentIntent got canceled after the fact
                            // (e.g. the capture call failed and this is Stripe confirming it) —
                            // free the ticket immediately rather than waiting on the TTL sweep.
                            await ReleaseHoldAsync(ticket, stripeEvent.Type);
                            break;

                        default:
                            logger.LogInformation(
                                "Ignoring Stripe event {EventType} for ticket {TicketId}", stripeEvent.Type, ticket.Id);
                            break;
                    }
                }
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

    private async Task ClaimAndCaptureAsync(PaymentIntent paymentIntent)
    {
        if (!paymentIntent.Metadata.TryGetValue("ticketId", out var ticketIdRaw) ||
            !Guid.TryParse(ticketIdRaw, out var ticketId))
        {
            logger.LogWarning(
                "Payment intent {PaymentIntentId} was authorized but has no valid ticketId metadata", paymentIntent.Id);
            return;
        }

        paymentIntent.Metadata.TryGetValue("buyerName", out var buyerName);
        paymentIntent.Metadata.TryGetValue("buyerEmail", out var buyerEmail);

        // The claim happens here — the moment Stripe confirms this buyer's card is actually
        // good for the money — not when checkout started. Any number of buyers can be mid
        // checkout for the same ticket at once; the same atomic-UPDATE-wins pattern used
        // everywhere else in this service decides which one of them actually gets it.
        var reservedAt = DateTime.UtcNow;
        var reservationCutoff = reservedAt - ReservationTtl;
        var claimed = await dbContext.Tickets
            .Where(t => t.Id == ticketId &&
                (t.Status == TicketSellStatus.ForSale ||
                 (t.Status == TicketSellStatus.Reserved && t.ReservedAt < reservationCutoff)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Status, TicketSellStatus.Reserved)
                .SetProperty(t => t.ReservedAt, reservedAt)
                .SetProperty(t => t.BuyerName, buyerName)
                .SetProperty(t => t.BuyerEmail, buyerEmail)
                .SetProperty(t => t.StripePaymentIntentId, paymentIntent.Id));

        if (claimed == 0)
        {
            // Zero rows doesn't necessarily mean a different buyer won: Stripe webhooks are
            // at-least-once, so this could be a redelivery of an event this exact PaymentIntent
            // already won on a previous (possibly concurrent) delivery. Check who's actually
            // holding the ticket before assuming this delivery lost and canceling its own
            // winning payment out from under itself.
            var currentHolder = await dbContext.Tickets
                .Where(t => t.Id == ticketId)
                .Select(t => t.StripePaymentIntentId)
                .FirstOrDefaultAsync();

            if (currentHolder == paymentIntent.Id)
            {
                logger.LogInformation(
                    "Ignoring duplicate amount_capturable_updated for payment intent {PaymentIntentId}; already claimed",
                    paymentIntent.Id);
                return;
            }

            // Someone else's card authorized first and already claimed this ticket (or it
            // doesn't exist / was pulled). This buyer's card was never actually charged —
            // release the hold Stripe placed on it rather than leaving it to expire on its own.
            logger.LogInformation(
                "Ticket {TicketId} was not available to claim for payment intent {PaymentIntentId}; canceling",
                ticketId, paymentIntent.Id);
            await CancelLosingPaymentIntentAsync(paymentIntent.Id, ticketId);
            return;
        }

        try
        {
            await new PaymentIntentService().CaptureAsync(paymentIntent.Id);
            logger.LogInformation(
                "Captured payment intent {PaymentIntentId} for ticket {TicketId}", paymentIntent.Id, ticketId);
        }
        catch (StripeException ex)
        {
            // No money was taken. Unlike a lost claim, this ticket really was reserved for this
            // buyer a moment ago — release it back to ForSale immediately rather than leaving
            // other buyers to wait on the TTL sweep for what's already a known dead end.
            logger.LogError(
                ex, "Failed to capture authorized payment intent {PaymentIntentId} for ticket {TicketId}; releasing hold",
                paymentIntent.Id, ticketId);
            await dbContext.Tickets
                .Where(t => t.Id == ticketId && t.Status == TicketSellStatus.Reserved && t.StripePaymentIntentId == paymentIntent.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Status, TicketSellStatus.ForSale)
                    .SetProperty(t => t.ReservedAt, (DateTime?)null)
                    .SetProperty(t => t.BuyerName, (string?)null)
                    .SetProperty(t => t.BuyerEmail, (string?)null)
                    .SetProperty(t => t.StripePaymentIntentId, (string?)null));
        }
    }

    private async Task CancelLosingPaymentIntentAsync(string paymentIntentId, Guid ticketId)
    {
        try
        {
            await new PaymentIntentService().CancelAsync(paymentIntentId);
        }
        catch (StripeException ex)
        {
            logger.LogError(
                ex, "Failed to cancel losing payment intent {PaymentIntentId} for ticket {TicketId}",
                paymentIntentId, ticketId);
        }
    }

    private async Task MarkTicketSoldAsync(Ticket ticket, PaymentIntent paymentIntent)
    {
        if (ticket.Status == TicketSellStatus.Sold)
        {
            // Stripe webhooks are at-least-once delivery; a redelivered payment_intent.succeeded
            // for an already-sold ticket is expected, not an error.
            logger.LogInformation(
                "Ignoring duplicate payment_intent.succeeded for already-sold ticket {TicketId}", ticket.Id);
            return;
        }

        if (ticket.Status != TicketSellStatus.Reserved)
        {
            // The reservation must have expired and been released (or the ticket was otherwise
            // deleted/reposted) before this payment completed.
            logger.LogWarning(
                "Payment succeeded for ticket {TicketId} but it was {Status}, not Reserved; not marking as sold",
                ticket.Id, ticket.Status);
            return;
        }

        // Same atomic-claim pattern as ClaimAndCaptureAsync, applied to the Reserved -> Sold
        // transition: Stripe webhooks are at-least-once delivery, so two concurrent redeliveries
        // of the same event could otherwise both read Status == Reserved before either write
        // lands and both send duplicate sold/purchase emails. The conditional UPDATE makes only
        // one delivery win the flip; the other sees 0 rows affected and no-ops.
        var sold = await dbContext.Tickets
            .Where(t => t.Id == ticket.Id && t.Status == TicketSellStatus.Reserved)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Status, TicketSellStatus.Sold)
                .SetProperty(t => t.SoldAt, DateTime.UtcNow)
                .SetProperty(t => t.SoldAtPriceUsd, paymentIntent.Amount / 100m));

        if (sold == 0)
        {
            logger.LogInformation(
                "Ticket {TicketId} was already transitioned away from Reserved by a concurrent webhook delivery; skipping",
                ticket.Id);
            return;
        }

        logger.LogInformation(
            "Marked ticket {TicketId} as sold from payment intent {PaymentIntentId}", ticket.Id, paymentIntent.Id);
        await PublishSoldNotificationsAsync(ticket, paymentIntent);
    }

    private async Task ReleaseHoldAsync(Ticket ticket, string reason)
    {
        var released = await dbContext.Tickets
            .Where(t => t.Id == ticket.Id && t.Status == TicketSellStatus.Reserved)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Status, TicketSellStatus.ForSale)
                .SetProperty(t => t.ReservedAt, (DateTime?)null)
                .SetProperty(t => t.BuyerName, (string?)null)
                .SetProperty(t => t.BuyerEmail, (string?)null)
                .SetProperty(t => t.StripePaymentIntentId, (string?)null));

        if (released > 0)
        {
            logger.LogInformation(
                "Released ticket {TicketId} back to ForSale after Stripe event {Reason}", ticket.Id, reason);
        }
    }

    private async Task PublishSoldNotificationsAsync(Ticket ticket, PaymentIntent paymentIntent)
    {
        try
        {
            var date = ticket.TicketDateTime!.Value.ToString("yyyy-MM-dd");
            var paymentDetails = PaymentInfoFormatter.Describe(ticket.PaymentInfo);

            await soldNotificationPublisher.PublishAsync(new SendEmailMessage
            {
                To = ticket.SellerEmail,
                Subject = "Your ticket has sold | تم بيع تذكرتك",
                Body = $"""
                    Good news — your ticket for {date} has sold on Manassa Ticket Exchange. We are now processing your payment and will be in touch shortly with the transfer.

                    You will receive: {ticket.SellerAskedPriceJod:0.00} JOD
                    Payout method: {paymentDetails}

                    ---

                    أخبار سارة — تم بيع تذكرتك بتاريخ {date} عبر منصة Manassa Ticket Exchange. نحن الآن بصدد معالجة عملية الدفع الخاصة بك وسنتواصل معك قريبًا بخصوص التحويل.

                    المبلغ الذي ستحصل عليه: {ticket.SellerAskedPriceJod:0.00} دينار أردني
                    طريقة استلام المبلغ: {paymentDetails}
                    """
            });

            if (ticket.BuyerEmail is null)
            {
                logger.LogWarning("Ticket {TicketId} was sold but has no BuyerEmail; skipping buyer notification", ticket.Id);
                return;
            }

            // paymentIntent.Amount is Stripe's own record of what was actually captured — used
            // directly rather than re-deriving from ticket price, so the receipt always matches
            // the real charge regardless of what it's composed of.
            var totalUsd = paymentIntent.Amount / 100m;
            var currency = (paymentIntent.Currency ?? stripeOptions.Value.Currency).ToUpperInvariant();

            await purchasedNotificationPublisher.PublishAsync(new SendEmailMessage
            {
                To = ticket.BuyerEmail,
                Subject = "Your Manassa Ticket Exchange purchase | عملية الشراء الخاصة بك",
                Body = $"""
                    Thanks for your purchase! Your ticket for {date} is attached.

                    Receipt
                    Amount charged: {totalUsd:0.00} {currency}

                    ---

                    شكرًا لعملية الشراء! تذكرتك بتاريخ {date} مرفقة مع هذه الرسالة.

                    إيصال الدفع
                    المبلغ المدفوع: {totalUsd:0.00} {currency}
                    """,
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
