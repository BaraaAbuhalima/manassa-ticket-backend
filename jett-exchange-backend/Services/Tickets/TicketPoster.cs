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
    ILogger<TicketPoster> logger)
    : ITicketPoster
{
    public async Task<ApiResponse<PostTicketResponse>> PostTicketAsync(PostTicketRequest request)
    {
        var extracted = await ExtractAndVerifyAsync(request.File);
        if (extracted is null)
        {
            return PostTicketFailedResponse();
        }

        var (ticketId, verifiedTicket) = extracted.Value;
        var ticket = await SaveTicketAsync(request, ticketId, verifiedTicket);

        await PublishTicketAvailableAsync(ticket);

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

    private async Task<(string TicketId, VerifiedTicketDTO Verified)?> ExtractAndVerifyAsync(IFormFile file)
    {
        var tempPath = await storage.SavePdfAsync(file, options.Value.TempTicketUploadPath);
        var ticketInfo = await dataExtractor.ExtractTicketAsync(tempPath);
        await storage.DeleteAsync(tempPath);

        if (!ticketInfo.Success)
        {
            return null;
        }

        var verifiedTicket = await ticketVerifier.VerifyTicketAsync(ticketInfo.BarCode);
        if (!verifiedTicket.Success || verifiedTicket.BarCode != ticketInfo.BarCode)
        {
            return null;
        }

        return (ticketInfo.TicketId, verifiedTicket);
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
            TotalPrice = verifiedTicket.TotalPrice,
            SellerName = request.SellerName,
            SellerEmail = request.SellerEmail,
            SellerPhone = request.SellerPhone,
            PaymentMethod = request.PaymentMethod,
            PaymentInfo = BuildPaymentInfo(request),
            Pin = pinGenerator.Generate(16),
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

    private static PaymentInfo BuildPaymentInfo(PostTicketRequest request)
    {
        var paymentInfoRequest = request.PaymentInfoRequest;

        return request.PaymentMethod switch
        {
            PaymentMethod.Iban => new BankTransferInfo
            {
                BankDetails = paymentInfoRequest.BankDetails!
            },
            PaymentMethod.Reflect => new Reflect
            {
                PhoneNumber = paymentInfoRequest.PhoneNumber!
            },
            PaymentMethod.Phone => new PhoneTransfer
            {
                PhoneNumber = paymentInfoRequest.PhoneNumber!
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(request), request.PaymentMethod, "Unsupported payment method")
        };
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
}
