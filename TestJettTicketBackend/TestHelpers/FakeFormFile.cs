using Microsoft.AspNetCore.Http;

namespace TestJettTicketBackend.TestHelpers;

public static class FakeFormFile
{
    public static IFormFile CreatePdf(string fileName = "ticket.pdf", byte[]? content = null, string contentType = "application/pdf")
    {
        content ??= [1, 2, 3];
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}