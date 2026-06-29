namespace jett_exchange_backend.Services;

public class TicketLocalFileStorage : IFileStorage
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


    public async Task<bool> DeleteAsync(string filePath)
    {



        if (!File.Exists(filePath))
        {
            return false;
        }

        File.Delete(filePath);

        await Task.CompletedTask; // Keeps the async signature

        return true;
    }
}