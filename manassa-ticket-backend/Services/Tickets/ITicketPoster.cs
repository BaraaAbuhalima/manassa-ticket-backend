using manassa_ticket_backend.Common;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.DTOs.Responses;

namespace manassa_ticket_backend.Services.Tickets;

public interface ITicketPoster
{
    Task<ApiResponse<CreateUploadUrlResponse>> CreateUploadUrlAsync();
    Task<ApiResponse<PostTicketResponse>> PostTicketAsync(PostTicketRequest request);
}
