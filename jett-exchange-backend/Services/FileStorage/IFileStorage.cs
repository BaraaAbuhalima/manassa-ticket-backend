namespace jett_exchange_backend.Services.FileStorage;

public interface IFileStorage
{
    Task<string> SavePdfAsync(IFormFile file, string path);
    Task<bool> DeleteAsync(string path);
}