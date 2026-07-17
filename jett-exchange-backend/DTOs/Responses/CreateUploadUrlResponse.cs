namespace jett_exchange_backend.DTOs.Responses;

public class CreateUploadUrlResponse
{
    public required string FileKey { get; set; }
    public required string UploadUrl { get; set; }
}
