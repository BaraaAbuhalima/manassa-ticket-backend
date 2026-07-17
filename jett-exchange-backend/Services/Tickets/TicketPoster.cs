using jett_exchange_backend.Common;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.DTOs.Responses;
using jett_exchange_backend.Helpers;
using jett_exchange_backend.Services.FileStorage;
using Microsoft.Extensions.Options;

namespace jett_exchange_backend.Services.Tickets;

public class TicketPoster(
    IOptions<StorageOptions> options,
    IFileStorage storage,
    IRandomPinGenerator pinGenerator,
    ITicketProcessor ticketProcessor)
    : ITicketPoster
{
    private static readonly TimeSpan UploadUrlExpiry = TimeSpan.FromMinutes(15);

    public async Task<ApiResponse<CreateUploadUrlResponse>> CreateUploadUrlAsync()
    {
        var (key, uploadUrl) = await storage.CreatePresignedUploadUrlAsync(options.Value.PermanentUploadPath, UploadUrlExpiry);

        return new ApiResponse<CreateUploadUrlResponse>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Upload URL created",
            Data = new CreateUploadUrlResponse
            {
                FileKey = key,
                UploadUrl = uploadUrl
            },
            Links = new Dictionary<string, string>
            {
                { "complete", "/api/ticket" },
            }
        };
    }

    public async Task<ApiResponse<PostTicketResponse>> PostTicketAsync(PostTicketRequest request)
    {
        var submission = new TicketSubmission
        {
            TicketRowId = Guid.NewGuid(),
            SellerName = request.SellerName,
            SellerEmail = request.SellerEmail,
            SellerPhone = request.SellerPhone,
            TotalPriceJod = request.Price,
            TotalPriceUsd = request.Price * CurrencyConversion.JodToUsdRate,
            PaymentMethod = request.PaymentMethod,
            PaymentInfo = PaymentInfoMapper.Build(request.PaymentMethod, request.PaymentInfoRequest),
            Pin = pinGenerator.Generate(12),
            TicketFilePath = request.FileKey
        };

        // Blocks until extraction and verification finish (a few seconds) so the caller
        // gets the definitive outcome directly, rather than an "accepted" response they'd
        // have to poll to resolve. Nothing is written to the database until this returns
        // successfully.
        var result = await ticketProcessor.ProcessAsync(submission);

        if (!result.Success)
        {
            return new ApiResponse<PostTicketResponse>
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
                Success = false,
                Message = result.RejectionReason!,
                Errors = [result.RejectionReason!],
                Links = new Dictionary<string, string>
                {
                    { "home", "/home" },
                }
            };
        }

        return new ApiResponse<PostTicketResponse>
        {
            StatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = "Ticket posted successfully",
            Data = new PostTicketResponse
            {
                TicketId = result.Ticket!.Id,
                RefPin = result.Ticket.Pin,
                Status = result.Ticket.Status
            },
            Links = new Dictionary<string, string>
            {
                { "status", "/api/ticket/by-pin/" + result.Ticket.Pin },
            }
        };
    }
}
