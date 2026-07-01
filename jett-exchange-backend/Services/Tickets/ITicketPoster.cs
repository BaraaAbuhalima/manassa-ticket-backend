using jett_exchange_backend.Common;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.DTOs.Responses;

namespace jett_exchange_backend.Services.Tickets;

public interface ITicketPoster
{
    Task<ApiResponse<PostTicketResponse>> PostTicketAsync(PostTicketRequest request);
}
