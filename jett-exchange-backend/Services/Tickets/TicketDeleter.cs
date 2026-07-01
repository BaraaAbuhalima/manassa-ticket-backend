using jett_exchange_backend.Common;
using jett_exchange_backend.Data;
using jett_exchange_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Services.Tickets;

public class TicketDeleter(AppDbContext dbContext, ITicketDeleteTokenService deleteTokenService) : ITicketDeleter
{
    public async Task<ApiResponse<string>> DeleteByIdAsync(Guid id)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        return await DeleteTicketAsync(ticket);
    }

    public async Task<ApiResponse<string>> DeleteByTokenAsync(string token)
    {
        var ticketId = deleteTokenService.ValidateAndGetTicketId(token);
        if (ticketId is null)
        {
            return new ApiResponse<string>
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Success = false,
                Message = "Invalid or expired delete token",
                Errors = ["Invalid or expired delete token"]
            };
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        return await DeleteTicketAsync(ticket);
    }

    private async Task<ApiResponse<string>> DeleteTicketAsync(Ticket? ticket)
    {
        if (ticket is null)
        {
            return TicketResponses.NotFound<string>();
        }

        ticket.Status = TicketSellStatus.Deleted;
        dbContext.Tickets.Update(ticket);
        await dbContext.SaveChangesAsync();

        return new ApiResponse<string>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Ticket deleted successfully",
        };
    }
}
