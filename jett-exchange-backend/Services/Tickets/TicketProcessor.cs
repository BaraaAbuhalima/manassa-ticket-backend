using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.TicketExtraction;
using jett_exchange_backend.Messaging;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.FileStorage;
using jett_exchange_backend.Services.TicketExtraction;
using jett_exchange_backend.Services.TicketVerification;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Services.Tickets;

public class TicketProcessor(
    AppDbContext dbContext,
    IFileStorage storage,
    ITicketDataExtractor dataExtractor,
    ITicketVerifier ticketVerifier,
    ITicketAvailablePublisher availablePublisher,
    IEmailMessagePublisher emailPublisher,
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

        var alreadyPosted = await dbContext.Tickets
            .AnyAsync(t => t.TicketId == ticketInfo.TicketId, cancellationToken);
        if (alreadyPosted)
        {
            return await RejectAsync(submission, "This ticket has already been posted.", cancellationToken);
        }

        var ticket = new Ticket
        {
            Id = submission.TicketRowId,
            SellerName = submission.SellerName,
            SellerEmail = submission.SellerEmail,
            SellerPhone = submission.SellerPhone,
            TotalPriceJod = submission.TotalPriceJod,
            TotalPriceUsd = submission.TotalPriceUsd,
            PaymentMethod = submission.PaymentMethod,
            PaymentInfo = submission.PaymentInfo,
            Pin = submission.Pin,
            TicketFilePath = submission.TicketFilePath,
            TicketId = ticketInfo.TicketId,
            OriginalOwnerName = verifiedTicket.OriginalOwnerName,
            OriginalOwnerPassportNumber = verifiedTicket.OriginalOwnerPassportNumber,
            NumberOfBags = verifiedTicket.NumberOfBags,
            OriginalPrice = verifiedTicket.TotalPrice,
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
            var paymentDetails = ticket.PaymentInfo switch
            {
                BankTransferInfo b => $"IBAN transfer - {b.BankDetails.AccountHolderName}, {b.BankDetails.BankName} ({b.BankDetails.Country}), account {b.BankDetails.AccountNumber}",
                Reflect r => $"Reflect - {r.PhoneNumber}",
                PhoneTransfer p => $"Phone transfer - {p.PhoneNumber}",
                _ => "N/A"
            };

            await emailPublisher.PublishAsync(new SendEmailMessage
            {
                To = ticket.SellerEmail,
                Subject = "Your ticket has been posted - Jett Ticket Exchange",
                Body = $"""
                    Hi {ticket.SellerName},

                    Your ticket has been posted successfully. Here's a confirmation of what you submitted:

                    Ticket reference: {ticket.TicketId}
                    Flight date: {ticket.TicketDateTime:yyyy-MM-dd}
                    Number of bags: {ticket.NumberOfBags}
                    Price: {ticket.TotalPriceUsd} USD ({ticket.TotalPriceJod} JOD)
                    Seller name: {ticket.SellerName}
                    Seller email: {ticket.SellerEmail}
                    Seller phone: {ticket.SellerPhone}
                    Payment method: {paymentDetails}

                    Your PIN: {ticket.Pin}
                    Keep this PIN safe - you'll need it to look up, edit, or delete this listing later.
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
                Subject = "Your ticket listing could not be posted - Jett Ticket Exchange",
                Body = $"""
                    Hi {submission.SellerName},

                    We weren't able to post your ticket listing: {reason}

                    Feel free to try posting it again.
                    """
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish rejection email for ticket {TicketRowId}", submission.TicketRowId);
        }
    }
}
