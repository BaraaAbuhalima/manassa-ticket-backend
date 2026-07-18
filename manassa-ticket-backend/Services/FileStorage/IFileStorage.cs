namespace manassa_ticket_backend.Services.FileStorage;

public interface IFileStorage
{
    Task<(string Key, string UploadUrl)> CreatePresignedUploadUrlAsync(string keyPrefix, TimeSpan expiry);
    Task<string> CreatePresignedDownloadUrlAsync(string key, TimeSpan expiry);
    Task<Stream?> OpenReadAsync(string key);
    Task<bool> DeleteAsync(string key);
}
