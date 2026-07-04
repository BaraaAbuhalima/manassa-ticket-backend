using jett_exchange_backend.Common;
using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Messaging;
using jett_exchange_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Services.Tickets;

public class TicketDeleter(
    AppDbContext dbContext,
    ITicketDeleteTokenService deleteTokenService,
    ITicketAvailablePublisher availablePublisher,
    ILogger<TicketDeleter> logger)
    : ITicketDeleter
{
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
                Errors = ["Invalid or expired delete token"],
                Links = new Dictionary<string, string>
                {
                    { "home", "/home" },
                }
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
                Errors = ["Ticket has already been sold and cannot be deleted"],
                Links = new Dictionary<string, string>
                {
                    { "home", "/home" },
                }
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
                { "home", "/home" },
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
                    { "home", "/home" },
                    { "by-pin", "/api/ticket/by-pin/" + ticket.Pin },
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
                { "home", "/home" },
                { "by-pin", "/api/ticket/by-pin/" + ticket.Pin },
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
                    { "home", "/home" },
                    { "by-pin", "/api/ticket/by-pin/" + ticket.Pin },
                }
            };
        }

        if (request.Price is not null)
        {
            var maxPrice = ticket.OriginalPrice + 1;
            if (request.Price > maxPrice)
            {
                return new ApiResponse<string>
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Success = false,
                    Message = $"Price cannot exceed {maxPrice}",
                    Errors = [$"Price cannot exceed {maxPrice}"],
                    Links = new Dictionary<string, string>
                    {
                        { "home", "/home" },
                        { "by-pin", "/api/ticket/by-pin/" + ticket.Pin },
                    }
                };
            }

            ticket.TotalPrice = request.Price.Value;
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
                { "home", "/home" },
                { "by-pin", "/api/ticket/by-pin/" + ticket.Pin },
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
                Date = DateOnly.FromDateTime(ticket.TicketDateTime)
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
