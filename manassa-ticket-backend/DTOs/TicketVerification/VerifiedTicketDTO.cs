using System.Globalization;

namespace manassa_ticket_backend.DTOs.TicketVerification;

public class VerifiedTicketDTO
{
    public bool Success { get; set; } = true;
    public string BarCode { get; set; } = string.Empty;

    public string OriginalOwnerName { get; set; } = string.Empty;

    public string OriginalOwnerPassportNumber { get; set; } = string.Empty;

    public DateTime TicketDateTime { get; set; }

    public int NumberOfBags { get; set; }
    
    public decimal TotalPriceJod { get; set; }

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
        date = date.AddHours(5);
        var time = TimeSpan.Parse(response.Details.Data.Booking.Travel_time_from);

        // TicketDateTime is stored as the bus's wall-clock travel date/time in a
        // "timestamp without time zone" column, so the Kind=Utc left over from the
        // deterministic parsing above must be stripped before it reaches the DbContext.
        TicketDateTime = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        var ticketAmountJod = decimal.Parse(ticket.Ticket_amount, CultureInfo.InvariantCulture);

        NumberOfBags = ticket.Count_luggage;

        TotalPriceJod = ticketAmountJod + (NumberOfBags * 2m);
    }

    public VerifiedTicketDTO()
    {
    }
}