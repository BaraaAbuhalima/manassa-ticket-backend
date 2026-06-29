using jett_exchange_backend.Configuration;
using jett_exchange_backend.Data;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Services;

public class TicketService : ITicketService
{
    private readonly AppDbContext _dbContext;
    private readonly IFileStorage _storage;
    private readonly StorageOptions _options;
    private readonly ITicketDataExtractor _dataExtractor;

    public TicketService(AppDbContext dbContext, StorageOptions options, IFileStorage storage,
        ITicketDataExtractor dataExtractor)
    {
        _dbContext = dbContext;
        _storage = storage;
        _options = options;
        _dataExtractor = dataExtractor;
    }

    public async Task<Ticket?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Tickets
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return false;
        }

        ticket.status = TicketSellStatus.Deleted;
        _dbContext.Tickets.Update(ticket);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteByRefAsync(string Ref)
    {
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(t => t.PinHashed == Ref);
        if (ticket is null)
        {
            return false;
        }

        ticket.status = TicketSellStatus.Deleted;
        _dbContext.Tickets.Update(ticket);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PostTicketAsync(PostTicketRequest request)
    {
        var path = await _storage.SavePdfAsync(request.File, _options.TempUploadPath);

        var ticketInfo = await _dataExtractor.ExtractTicketAsync(path);
        var fileDeleted = await _storage.DeleteAsync(path);
        if (!fileDeleted)
        {
            //do something later
        }

        path = await _storage.SavePdfAsync(request.File, _options.TicketsPath);


        var ticket = new Ticket
        {
            TicketId = null,
            OriginalOwnerName = null,
            OriginalOwnerPassportNumber = null,
            Price = 0,
            NumberOfBags = 0,
            TotalPrice = 0,
            SellerName = null,
            SellerEmail = null,
            SellerPhone = null,
            PaymentMethod = PaymentMethod.Iban,
            PaymentInfo = null,
            PinHashed = null,
            status = TicketSellStatus.Deleted,
            TicketFilePath = path
        };

        await _dbContext.Tickets.AddAsync(ticket);
        await _dbContext.SaveChangesAsync();
        return true;


    }
}