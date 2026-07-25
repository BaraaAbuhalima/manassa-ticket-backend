using manassa_ticket_backend.Common;
using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.DTOs.TicketExtraction;
using manassa_ticket_backend.Messaging;
using manassa_ticket_backend.Models;
using manassa_ticket_backend.Services.FileStorage;
using manassa_ticket_backend.Services.TicketExtraction;
using manassa_ticket_backend.Services.TicketVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.Tickets;

public class TicketProcessor(
    AppDbContext dbContext,
    IFileStorage storage,
    ITicketDataExtractor dataExtractor,
    ITicketVerifier ticketVerifier,
    ITicketAvailablePublisher availablePublisher,
    IEmailMessagePublisher emailPublisher,
    IOptions<TicketPricingOptions> pricingOptions,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<TicketProcessor> logger)
    : ITicketProcessor
{
    public async Task<TicketProcessingResult> ProcessAsync(TicketSubmission submission, CancellationToken cancellationToken = default)
    {
        var fileStream = await storage.OpenReadAsync(submission.TicketFilePath);
        if (fileStream is null)
        {
            return await RejectAsync(submission, "The uploaded file could not be found in storage.", cancellationToken);
        }

        PdfTicketDTO ticketInfo;
        await using (fileStream)
        {
            ticketInfo = await dataExtractor.ExtractTicketAsync(fileStream, Path.GetFileName(submission.TicketFilePath));
        }

        if (!ticketInfo.Success)
        {
            return await RejectAsync(submission, "Failed to extract ticket information from the PDF.", cancellationToken);
        }

        var verifiedTicket = await ticketVerifier.VerifyTicketAsync(ticketInfo.BarCode);
        if (!verifiedTicket.Success || verifiedTicket.BarCode != ticketInfo.BarCode)
        {
            return await RejectAsync(submission, "Ticket verification failed.", cancellationToken);
        }

        if (ticketInfo.TicketDateTime is null || ticketInfo.TicketDateTime.Value.Date != verifiedTicket.TicketDateTime.Date)
        {
            return await RejectAsync(submission, "Ticket date does not match the verified booking date.", cancellationToken);
        }

        var alreadyPosted = await dbContext.Tickets
            .AnyAsync(t => t.TicketId == ticketInfo.TicketId, cancellationToken);
        if (alreadyPosted)
        {
            return await RejectAsync(submission, "This ticket has already been posted.", cancellationToken);
        }

        var maxAskingPriceIncreaseJod = pricingOptions.Value.MaxAskingPriceIncreaseJod;
        var maxAskingPriceJod = verifiedTicket.TotalPriceJod + maxAskingPriceIncreaseJod;
        if (submission.TotalPriceJod > maxAskingPriceJod)
        {
            return await RejectAsync(
                submission,
                $"Asking price cannot exceed the original ticket price by more than {maxAskingPriceIncreaseJod:0.##} JOD (max {maxAskingPriceJod:0.00} JOD).",
                cancellationToken);
        }

        var ticket = new Ticket
        {
            Id = submission.TicketRowId,
            SellerName = submission.SellerName,
            SellerEmail = submission.SellerEmail,
            SellerPhone = submission.SellerPhone,
            SellerAskedPriceJod = submission.TotalPriceJod,
            PaymentMethod = submission.PaymentMethod,
            PaymentInfo = submission.PaymentInfo,
            Pin = submission.Pin,
            TicketFilePath = submission.TicketFilePath,
            TicketId = ticketInfo.TicketId,
            OriginalOwnerName = verifiedTicket.OriginalOwnerName,
            OriginalOwnerPassportNumber = verifiedTicket.OriginalOwnerPassportNumber,
            NumberOfBags = verifiedTicket.NumberOfBags,
            OriginalPrice = verifiedTicket.TotalPriceJod,
            TicketDateTime = verifiedTicket.TicketDateTime,
            Status = TicketSellStatus.ForSale
        };

        await dbContext.Tickets.AddAsync(ticket, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishTicketAvailableAsync(ticket, cancellationToken);
        await PublishPostConfirmationAsync(ticket, cancellationToken);

        return new TicketProcessingResult { Success = true, Ticket = ticket };
    }

    private async Task<TicketProcessingResult> RejectAsync(TicketSubmission submission, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(submission.TicketFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete rejected ticket's file {TicketFilePath} from storage", submission.TicketFilePath);
        }

        await PublishRejectionEmailAsync(submission, reason, cancellationToken);

        return new TicketProcessingResult { Success = false, RejectionReason = reason };
    }

    private async Task PublishTicketAvailableAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        try
        {
            await availablePublisher.PublishAsync(new TicketAvailableMessage
            {
                TicketId = ticket.Id,
                Date = DateOnly.FromDateTime(ticket.TicketDateTime!.Value)
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // A broker outage shouldn't fail a successful ticket post; subscribers
            // just won't be notified for this listing.
            logger.LogError(ex, "Failed to publish ticket-available message for ticket {TicketId}", ticket.Id);
        }
    }

    private async Task PublishPostConfirmationAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        try
        {
            var paymentDetails = PaymentInfoFormatter.Describe(ticket.PaymentInfo);

            await emailPublisher.PublishAsync(new SendEmailMessage
            {
                To = ticket.SellerEmail,
                From = smtpOptions.Value.FromAddress,
                Subject = "Your ticket has been posted - Manassa Ticket Exchange | تم نشر تذكرتك",
                Body = $"""
                    Hi {ticket.SellerName},

                    Your ticket has been posted successfully. Here's a confirmation of what you submitted:

                    Ticket reference: {ticket.TicketId}
                    Travel date: {ticket.TicketDateTime:yyyy-MM-dd}
                    Number of bags: {ticket.NumberOfBags}
                    Price: {ticket.SellerAskedPriceJod:0.00} JOD
                    Seller name: {ticket.SellerName}
                    Seller email: {ticket.SellerEmail}
                    Seller phone: {ticket.SellerPhone}
                    Payment method: {paymentDetails}

                    Your PIN: {ticket.Pin}
                    Keep this PIN safe - you'll need it to look up, edit, or delete this listing later.

                    ---

                    مرحبًا {ticket.SellerName}،

                    تم نشر تذكرتك بنجاح. فيما يلي تأكيد بالبيانات التي أدخلتها:

                    رقم التذكرة المرجعي: {ticket.TicketId}
                    تاريخ السفر: {ticket.TicketDateTime:yyyy-MM-dd}
                    عدد الحقائب: {ticket.NumberOfBags}
                    السعر: {ticket.SellerAskedPriceJod:0.00} دينار أردني
                    اسم البائع: {ticket.SellerName}
                    البريد الإلكتروني للبائع: {ticket.SellerEmail}
                    رقم هاتف البائع: {ticket.SellerPhone}
                    طريقة استلام المبلغ: {paymentDetails}

                    الرقم السري (PIN) الخاص بك: {ticket.Pin}
                    احتفظ بهذا الرقم في مكان آمن - ستحتاجه للبحث عن هذا الإعلان أو تعديله أو حذفه لاحقًا.
                    """
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // A broker outage shouldn't fail a successful ticket post; the seller
            // just won't get a confirmation email for this listing.
            logger.LogError(ex, "Failed to publish post-confirmation email for ticket {TicketId}", ticket.Id);
        }
    }

    private async Task PublishRejectionEmailAsync(TicketSubmission submission, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await emailPublisher.PublishAsync(new SendEmailMessage
            {
                To = submission.SellerEmail,
                From = smtpOptions.Value.FromAddress,
                Subject = "Your ticket listing could not be posted - Manassa Ticket Exchange | تعذّر نشر تذكرتك",
                Body = $"""
                    Hi {submission.SellerName},

                    We weren't able to post your ticket listing: {reason}

                    Feel free to try posting it again.

                    ---

                    مرحبًا {submission.SellerName}،

                    لم نتمكن من نشر إعلان تذكرتك: {reason}

                    لا تتردد في إعادة المحاولة.
                    """
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish rejection email for ticket {TicketRowId}", submission.TicketRowId);
        }
    }
}
