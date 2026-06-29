using jett_exchange_backend.DTOs;

namespace jett_exchange_backend.Services;

public interface ITicketDataExtractor
{
    Task<RawTicketDTO> ExtractTicketAsync(string filePath);
}