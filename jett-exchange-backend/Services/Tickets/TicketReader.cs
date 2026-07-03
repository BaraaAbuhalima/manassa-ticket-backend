using jett_exchange_backend.Common;
using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.Responses;
using jett_exchange_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Services.Tickets;

public class TicketReader(AppDbContext dbContext, ITicketDeleteTokenService deleteTokenService) : ITicketReader
{
    private const int PageSize = 20;

    public async Task<ApiResponse<GetTicketByIdResponse>> GetByIdAsync(Guid id)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return TicketResponses.NotFound<GetTicketByIdResponse>();
        }

        return new ApiResponse<GetTicketByIdResponse>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Ticket retrieved successfully",
            Data = ToResponse(ticket),
            Links = new Dictionary<string, string>
            {
                { "self", "/ticket?id=" + id },
                { "buy", "/ticket/buy?id=" + id }
            }
        };
    }

    public async Task<ApiResponse<Ticket>> GetByPinAsync(string pin)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Pin == pin);
        if (ticket is null)
        {
            return TicketResponses.NotFound<Ticket>();
        }

        var deleteToken = deleteTokenService.GenerateToken(ticket.Id);

        return new ApiResponse<Ticket>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Ticket retrieved successfully",
            Data = ticket,
            Links = new Dictionary<string, string>
            {
                { "delete", "/api/ticket/" + deleteToken }
            }
        };
    }

    public async Task<ApiResponse<List<GetTicketByIdResponse>>> GetForDateAsync(DateOnly date, int page)
    {
        return await GetPagedByDateRangeAsync(date, date, page);
    }

    public async Task<ApiResponse<List<GetTicketByIdResponse>>> GetForDateRangeAsync(DateOnly startDate, DateOnly endDate, int page)
    {
        if (startDate > endDate)
        {
            return new ApiResponse<List<GetTicketByIdResponse>>
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Success = false,
                Message = "startDate must not be after endDate",
                Errors = ["startDate must not be after endDate"]
            };
        }

        return await GetPagedByDateRangeAsync(startDate, endDate, page);
    }

    private async Task<ApiResponse<List<GetTicketByIdResponse>>> GetPagedByDateRangeAsync(DateOnly startDate, DateOnly endDate, int page)
    {
        page = page < 1 ? 1 : page;

        var rangeStart = startDate.ToDateTime(TimeOnly.MinValue);
        var rangeEndExclusive = endDate.ToDateTime(TimeOnly.MinValue).AddDays(1);

        var query = dbContext.Tickets
            .Where(t => t.Status == TicketSellStatus.ForSale)
            .Where(t => t.TicketDateTime >= rangeStart && t.TicketDateTime < rangeEndExclusive)
            .OrderBy(t => t.TicketDateTime)
            .ThenBy(t => t.CreatedAt);

        var totalCount = await query.CountAsync();
        var tickets = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        return new ApiResponse<List<GetTicketByIdResponse>>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Tickets retrieved successfully",
            Data = tickets.Select(ToResponse).ToList(),
            Meta = new MetaData
            {
                Timestamp = DateTime.UtcNow,
                Page = page,
                PageSize = PageSize,
                TotalCount = totalCount
            }
        };
    }

    private static GetTicketByIdResponse ToResponse(Ticket ticket) => new()
    {
        Id = ticket.Id,
        TicketDateTime = ticket.TicketDateTime,
        NumberOfBags = ticket.NumberOfBags,
        TotalPrice = ticket.TotalPrice
    };
}
