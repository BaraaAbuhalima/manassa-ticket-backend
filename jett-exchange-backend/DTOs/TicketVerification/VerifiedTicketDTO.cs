using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using jett_exchange_backend.Common;

namespace jett_exchange_backend.DTOs.TicketVerification;

public class VerifiedTicketDTO
{
    public bool Success { get; set; } = true;
    public string BarCode { get; set; } = string.Empty;

    public string OriginalOwnerName { get; set; } = string.Empty;

    public string OriginalOwnerPassportNumber { get; set; } = string.Empty;

    public DateTime TicketDateTime { get; set; }

    [Column(TypeName = "decimal(3,2)")]
    public decimal Price { get; set; }

    public int NumberOfBags { get; set; }

    public decimal TotalPrice { get; set; }

    public VerifiedTicketDTO(TransferTicketDTO response)
    {
        var ticket = response.Details.Data;

        BarCode = ticket.Barcode;
        OriginalOwnerName = ticket.Name;
        OriginalOwnerPassportNumber = ticket.Passport_number;

        // AssumeUniversal + AdjustToUniversal makes parsing deterministic regardless of the
        // server's local timezone: an unmarked date/time is treated as already UTC, and one
        // with an offset/Z is converted to UTC rather than silently becoming Kind=Local.
        var date = DateTime.Parse(
            response.Details.Data.Booking.Travel_date,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var time = TimeSpan.Parse(response.Details.Data.Booking.Travel_time_from);

        TicketDateTime = date.Date.Add(time);

        Price = decimal.Parse(ticket.Ticket_amount, CultureInfo.InvariantCulture) * CurrencyConversion.JodToUsdRate;

        NumberOfBags = ticket.Count_luggage;

        TotalPrice = Price + (NumberOfBags * 2m);
    }

    public VerifiedTicketDTO()
    {
    }
}