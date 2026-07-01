namespace jett_exchange_backend.Services.FileStorage;

public class LocalFileStorage : IFileStorage
{
    public async Task<string> SavePdfAsync(IFormFile file, string path)
    {
        Directory.CreateDirectory(path);

        var fileName = $"{Guid.NewGuid()}.pdf";
        var filePath = Path.Combine(path, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return filePath;
    }

    public Task<bool> DeleteAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        File.Delete(filePath);
        return Task.FromResult(true);
    }
}