using jett_exchange_backend.Common;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.DTOs.Responses;
using jett_exchange_backend.DTOs.TicketVerification;
using jett_exchange_backend.Helpers;
using jett_exchange_backend.Messaging;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services.FileStorage;
using jett_exchange_backend.Services.TicketExtraction;
using jett_exchange_backend.Services.TicketVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace jett_exchange_backend.Services.Tickets;

public class TicketPoster(
    AppDbContext dbContext,
    IOptions<StorageOptions> options,
    IFileStorage storage,
    ITicketDataExtractor dataExtractor,
    ITicketVerifier ticketVerifier,
    IRandomPinGenerator pinGenerator,
    ITicketAvailablePublisher availablePublisher,
    IEmailMessagePublisher emailPublisher,
    ILogger<TicketPoster> logger)
    : ITicketPoster
{
    public async Task<ApiResponse<PostTicketResponse>> PostTicketAsync(PostTicketRequest request)
    {
        var tempPath = await storage.SavePdfAsync(request.File, options.Value.TempTicketUploadPath);
        var ticketInfo = await dataExtractor.ExtractTicketAsync(tempPath);
        await storage.DeleteAsync(tempPath);

        if (!ticketInfo.Success)
        {
            return PostTicketFailedResponse();
        }

        var verifiedTicket = await ticketVerifier.VerifyTicketAsync(ticketInfo.BarCode);
        if (!verifiedTicket.Success || verifiedTicket.BarCode != ticketInfo.BarCode)
        {
            return VerificationFailedResponse();
        }

        if (await dbContext.Tickets.AnyAsync(t => t.TicketId == ticketInfo.TicketId))
        {
            return TicketAlreadyPostedResponse();
        }

        var ticket = await SaveTicketAsync(request, ticketInfo.TicketId, verifiedTicket);

        await PublishTicketAvailableAsync(ticket);
        await PublishPostConfirmationAsync(ticket);

        var postedTicket = new PostTicketResponse
        {
            TicketId = ticket.Id,
            RefPin = ticket.Pin
        };

        return new ApiResponse<PostTicketResponse>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Ticket posted successfully",
            Data = postedTicket,
            Links = new Dictionary<string, string>
            {
                { "posted-ticket", "/ticket?id=" + postedTicket.TicketId },
                { "home", "/home" },
            }
        };
    }

    private async Task<Ticket> SaveTicketAsync(PostTicketRequest request, string ticketId, VerifiedTicketDTO verifiedTicket)
    {
        var permanentPath = await storage.SavePdfAsync(request.File, options.Value.PermanentUploadPath);

        var ticket = new Ticket
        {
            TicketId = ticketId,
            OriginalOwnerName = verifiedTicket.OriginalOwnerName,
            OriginalOwnerPassportNumber = verifiedTicket.OriginalOwnerPassportNumber,
            NumberOfBags = verifiedTicket.NumberOfBags,
            TotalPriceJod = request.Price,
            TotalPriceUsd = request.Price * CurrencyConversion.JodToUsdRate,
            OriginalPrice = verifiedTicket.TotalPrice,
            SellerName = request.SellerName,
            SellerEmail = request.SellerEmail,
            SellerPhone = request.SellerPhone,
            PaymentMethod = request.PaymentMethod,
            TicketDateTime = verifiedTicket.TicketDateTime,
            PaymentInfo = PaymentInfoMapper.Build(request.PaymentMethod, request.PaymentInfoRequest),
            Pin = pinGenerator.Generate(12),
            Status = TicketSellStatus.ForSale,
            TicketFilePath = permanentPath
        };

        await dbContext.Tickets.AddAsync(ticket);
        await dbContext.SaveChangesAsync();

        return ticket;
    }

    private async Task PublishTicketAvailableAsync(Ticket ticket)
    {
        try
        {
            await availablePublisher.PublishAsync(new TicketAvailableMessage
            {
                TicketId = ticket.Id,
                Date = DateOnly.FromDateTime(ticket.TicketDateTime)
            });
        }
        catch (Exception ex)
        {
            // A broker outage shouldn't fail a successful ticket post; subscribers
            // just won't be notified for this listing.
            logger.LogError(ex, "Failed to publish ticket-available message for ticket {TicketId}", ticket.Id);
        }
    }

    private async Task PublishPostConfirmationAsync(Ticket ticket)
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
            });
        }
        catch (Exception ex)
        {
            // A broker outage shouldn't fail a successful ticket post; the seller
            // just won't get a confirmation email for this listing.
            logger.LogError(ex, "Failed to publish post-confirmation email for ticket {TicketId}", ticket.Id);
        }
    }

    private static ApiResponse<PostTicketResponse> PostTicketFailedResponse()
    {
        return new ApiResponse<PostTicketResponse>
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            Success = false,
            Message = "Failed to Post ticket",
            Errors = ["Failed to Post ticket"],
            Links = new Dictionary<string, string>
            {
                { "home", "/home" },
                { "repost-ticket", "ticket/post" },
            }
        };
    }

    private static ApiResponse<PostTicketResponse> VerificationFailedResponse()
    {
        return new ApiResponse<PostTicketResponse>
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            Success = false,
            Message = "Ticket verification failed",
            Errors = ["Ticket verification failed"],
            Links = new Dictionary<string, string>
            {
                { "home", "/home" },
                { "repost-ticket", "ticket/post" },
            }
        };
    }

    private static ApiResponse<PostTicketResponse> TicketAlreadyPostedResponse()
    {
        return new ApiResponse<PostTicketResponse>
        {
            StatusCode = StatusCodes.Status409Conflict,
            Success = false,
            Message = "Ticket already posted",
            Errors = ["Ticket already posted"],
            Links = new Dictionary<string, string>
            {
                { "home", "/home" },
            }
        };
    }
}
