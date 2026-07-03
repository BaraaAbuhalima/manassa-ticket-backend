using jett_exchange_backend.Common;
using jett_exchange_backend.DTOs.Responses;
using jett_exchange_backend.Models;

namespace jett_exchange_backend.Services.Tickets;

public interface ITicketReader
{
    Task<ApiResponse<GetTicketByIdResponse>> GetByIdAsync(Guid id);
    Task<ApiResponse<Ticket>> GetByPinAsync(string pin);
    Task<ApiResponse<List<Ticket>>> GetForDateAsync(DateOnly date, int page);
    Task<ApiResponse<List<Ticket>>> GetForDateRangeAsync(DateOnly startDate, DateOnly endDate, int page);
}
