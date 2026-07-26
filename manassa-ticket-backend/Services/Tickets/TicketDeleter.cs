using manassa_ticket_backend.Common;
using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.DTOs.Responses;
using manassa_ticket_backend.Messaging;
using manassa_ticket_backend.Models;
using manassa_ticket_backend.Services.FileStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.Tickets;

public class TicketDeleter(
    AppDbContext dbContext,
    ITicketDeleteTokenService deleteTokenService,
    ITicketAvailablePublisher availablePublisher,
    IFileStorage fileStorage,
    IOptions<TicketPricingOptions> pricingOptions,
    ILogger<TicketDeleter> logger)
    : ITicketDeleter
{
    private static readonly TimeSpan DownloadUrlExpiry = TimeSpan.FromMinutes(15);

    public async Task<ApiResponse<TicketFileUrlResponse>> GetFileUrlByTokenAsync(string token)
    {
        var ticketId = deleteTokenService.ValidateAndGetTicketId(token);
        if (ticketId is null)
        {
            return new ApiResponse<TicketFileUrlResponse>
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Success = false,
                Message = "Invalid or expired delete token",
                Errors = ["Invalid or expired delete token"],

            };
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null)
        {
            return TicketResponses.NotFound<TicketFileUrlResponse>();
        }

        var downloadUrl = await fileStorage.CreatePresignedDownloadUrlAsync(ticket.TicketFilePath, DownloadUrlExpiry);

        return new ApiResponse<TicketFileUrlResponse>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Download URL created",
            Data = new TicketFileUrlResponse { DownloadUrl = downloadUrl }
        };
    }

    public async Task<ApiResponse<string>> DeleteByIdAsync(Guid id)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        return await DeleteTicketAsync(ticket);
    }

    public async Task<ApiResponse<string>> DeleteByTokenAsync(string token)
    {
        var (ticket, tokenError) = await ResolveTicketFromTokenAsync(token);
        return tokenError ?? await DeleteTicketAsync(ticket);
    }

    public async Task<ApiResponse<string>> RepublishByTokenAsync(string token)
    {
        var (ticket, tokenError) = await ResolveTicketFromTokenAsync(token);
        return tokenError ?? await RepublishTicketAsync(ticket);
    }

    public async Task<ApiResponse<string>> ModifyTicketByTokenAsync(string token, UpdateTicketRequest request)
    {
        var (ticket, tokenError) = await ResolveTicketFromTokenAsync(token);
        return tokenError ?? await ModifyTicketAsync(ticket, request);
    }

    private async Task<(Ticket? Ticket, ApiResponse<string>? Error)> ResolveTicketFromTokenAsync(string token)
    {
        var ticketId = deleteTokenService.ValidateAndGetTicketId(token);
        if (ticketId is null)
        {
            var error = new ApiResponse<string>
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Success = false,
                Message = "Invalid or expired delete token",
                Errors = ["Invalid or expired delete token"]
            };
            return (null, error);
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        return (ticket, null);
    }

    private async Task<ApiResponse<string>> DeleteTicketAsync(Ticket? ticket)
    {
        if (ticket is null)
        {
            return TicketResponses.NotFound<string>();
        }

        if (ticket.Status == TicketSellStatus.Sold)
        {
            return new ApiResponse<string>
            {
                StatusCode = StatusCodes.Status409Conflict,
                Success = false,
                Message = "Ticket has already been sold and cannot be deleted",
                Errors = ["Ticket has already been sold and cannot be deleted"]
            };
        }

        if (ticket.Status == TicketSellStatus.Reserved)
        {
            // A buyer's payment may be in flight against this exact row; deleting it here
            // would let the seller pull the ticket out from under a purchase that then
            // succeeds with no ticket to deliver.
            return new ApiResponse<string>
            {
                StatusCode = StatusCodes.Status409Conflict,
                Success = false,
                Message = "Ticket is currently being purchased and cannot be deleted",
                Errors = ["Ticket is currently being purchased and cannot be deleted"]
            };
        }

        ticket.Status = TicketSellStatus.Deleted;
        dbContext.Tickets.Update(ticket);
        await dbContext.SaveChangesAsync();

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Ticket deleted successfully",
            Links = new Dictionary<string, string>
            {
                { "republish", "/api/ticket/republish" },
            }
        };
    }

    private async Task<ApiResponse<string>> RepublishTicketAsync(Ticket? ticket)
    {
        if (ticket is null)
        {
            return TicketResponses.NotFound<string>();
        }

        if (ticket.Status != TicketSellStatus.Deleted)
        {
            return new ApiResponse<string>
            {
                StatusCode = StatusCodes.Status409Conflict,
                Success = false,
                Message = "Only a deleted ticket can be republished",
                Errors = ["Only a deleted ticket can be republished"],
                Links = new Dictionary<string, string>
                {
                    { "republish", "/api/ticket/republish" },
                    { "modify", "/api/ticket" },
                    { "download", "/api/ticket/file-url" }
                }
            };
        }

        ticket.Status = TicketSellStatus.ForSale;
        dbContext.Tickets.Update(ticket);
        await dbContext.SaveChangesAsync();

        await PublishTicketAvailableAsync(ticket);

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Ticket republished successfully",
            Links = new Dictionary<string, string>
            {
                { "republish", "/api/ticket/republish" },
                { "modify", "/api/ticket" },
                { "download", "/api/ticket/file-url" }
            }
        };
    }

    private async Task<ApiResponse<string>> ModifyTicketAsync(Ticket? ticket, UpdateTicketRequest request)
    {
        if (ticket is null)
        {
            return TicketResponses.NotFound<string>();
        }

        if (ticket.Status != TicketSellStatus.ForSale)
        {
            return new ApiResponse<string>
            {
                StatusCode = StatusCodes.Status409Conflict,
                Success = false,
                Message = "Only a ticket for sale can be modified",
                Errors = ["Only a ticket for sale can be modified"],
                Links = new Dictionary<string, string>
                {
                    { "republish", "/api/ticket/republish" },
                    { "modify", "/api/ticket" },
                    { "download", "/api/ticket/file-url" }
                }
            };
        }

        if (request.Price is not null)
        {
            // OriginalPrice is stored in JOD (matching the currency on the original ticket),
            // same as request.Price, so the cap is a direct JOD comparison — no conversion.
            var maxPriceJod = ticket.OriginalPrice + pricingOptions.Value.MaxAskingPriceIncreaseJod;
            if (request.Price.Value > maxPriceJod)
            {
                return new ApiResponse<string>
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Success = false,
                    Message = $"Price cannot exceed {maxPriceJod:0.00} JOD",
                    Errors = [$"Price cannot exceed {maxPriceJod:0.00} JOD"],
                    Links = new Dictionary<string, string>
                    {
                        { "republish", "/api/ticket/republish" },
                        { "modify", "/api/ticket" },
                        { "download", "/api/ticket/file-url" }
                    }
                };
            }

            ticket.SellerAskedPriceJod = request.Price.Value;
        }

        if (request.Payment is not null)
        {
            ticket.PaymentMethod = request.Payment.PaymentMethod;
            ticket.PaymentInfo = PaymentInfoMapper.Build(request.Payment.PaymentMethod, request.Payment.PaymentInfoRequest);
        }

        dbContext.Tickets.Update(ticket);
        await dbContext.SaveChangesAsync();

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Ticket updated successfully",
            Links = new Dictionary<string, string>
            {
                { "republish", "/api/ticket/republish" },
                { "modify", "/api/ticket" },
                { "download", "/api/ticket/file-url" }
            }
        };
    }

    private async Task PublishTicketAvailableAsync(Ticket ticket)
    {
        try
        {
            await availablePublisher.PublishAsync(new TicketAvailableMessage
            {
                TicketId = ticket.Id,
                Date = DateOnly.FromDateTime(ticket.TicketDateTime!.Value)
            });
        }
        catch (Exception ex)
        {
            // A broker outage shouldn't fail a successful republish; subscribers
            // just won't be notified for this listing.
            logger.LogError(ex, "Failed to publish ticket-available message for ticket {TicketId}", ticket.Id);
        }
    }
}
