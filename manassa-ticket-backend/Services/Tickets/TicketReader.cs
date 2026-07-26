using manassa_ticket_backend.Common;
using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.DTOs.Responses;
using manassa_ticket_backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.Tickets;

public class TicketReader(AppDbContext dbContext, IOptions<FeeOptions> feeOptions) : ITicketReader
{
    private const int PageSize = 20;

    public async Task<ApiResponse<GetTicketByIdResponse>> GetByIdAsync(Guid id)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        
        if (ticket is null || ticket.Status != TicketSellStatus.ForSale)
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

    public async Task<ApiResponse<GetTicketByPinResponse>> GetByPinAsync(string pin, string email)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Pin == pin && t.SellerEmail.ToLower() == email.ToLower());
        if (ticket is null)
        {
            return TicketResponses.NotFound<GetTicketByPinResponse>();
        }

        return new ApiResponse<GetTicketByPinResponse>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = ticket.RejectionReason ?? "Ticket retrieved successfully",
            Data = new GetTicketByPinResponse
            {
                Id = ticket.Id,
                TicketDateTime = ticket.TicketDateTime,
                NumberOfBags = ticket.NumberOfBags,
                SellerAskedPriceJod = ticket.SellerAskedPriceJod,
                Status = ticket.Status,
                SoldAt = ticket.SoldAt,
                SellerEmail = ticket.SellerEmail,
                SellerPhone = ticket.SellerPhone,
                PaymentMethod = ticket.PaymentMethod,
                PaymentInfo = ticket.PaymentInfo
            },
            Links = new Dictionary<string, string>
            {
                { "delete", "/api/ticket" },
                { "republish", "/api/ticket/republish" },
                { "modify", "/api/ticket" }
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
                Errors = ["startDate must not be after endDate"],
                Links = new Dictionary<string, string>
                {
                    { "home", "/home" },
                }
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
            // Cast to double: SQLite (used in tests) can't translate ORDER BY on a decimal column.
            .OrderBy(t => (double)t.SellerAskedPriceJod)
            .ThenBy(t => t.TicketDateTime)
            .ThenBy(t => t.CreatedAt);

        var totalCount = await query.CountAsync();
        var tickets = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        var links = new Dictionary<string, string>();
        if (page > 1)
        {
            links["prev"] = BuildRangeLink(startDate, endDate, page - 1);
        }
        if (page < totalPages)
        {
            links["next"] = BuildRangeLink(startDate, endDate, page + 1);
        }

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
            },
            Links = links
        };
    }

    private static string BuildRangeLink(DateOnly startDate, DateOnly endDate, int page) =>
        $"/api/ticket/range?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&page={page}";

    // Same fee math as TicketPurchaseService.CreatePaymentIntentAsync — a buyer should see the
    // price they'll actually be charged while browsing, not just find out about the fee once
    // they reach checkout.
    private GetTicketByIdResponse ToResponse(Ticket ticket)
    {
        var baseUsd = CurrencyConversion.JodToUsd(ticket.SellerAskedPriceJod);
        var fee = FeeCalculator.CalculateFeeUsd(baseUsd, feeOptions.Value.FlatFeeUsd, feeOptions.Value.PercentFee);
        var totalUsd = baseUsd + fee;
        var totalJod = Math.Round(totalUsd * CurrencyConversion.UsdToJodRate, 2, MidpointRounding.AwayFromZero);
        return new GetTicketByIdResponse
        {
            Id = ticket.Id,
            TicketDateTime = ticket.TicketDateTime,
            NumberOfBags = ticket.NumberOfBags,
            TotalPriceUsd = totalUsd,
            TotalPriceJod = totalJod
        };
    }
}
