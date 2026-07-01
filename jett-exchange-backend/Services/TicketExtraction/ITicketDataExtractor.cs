using jett_exchange_backend.DTOs.TicketExtraction;

namespace jett_exchange_backend.Services.TicketExtraction;

public interface ITicketDataExtractor
{
    Task<PdfTicketDTO> ExtractTicketAsync(string filePath);
}