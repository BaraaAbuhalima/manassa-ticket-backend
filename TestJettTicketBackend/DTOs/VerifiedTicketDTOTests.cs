using jett_exchange_backend.DTOs.TicketVerification;
using FluentAssertions;

namespace TestJettTicketBackend.DTOs;

public class VerifiedTicketDTOTests
{
    private static TransferTicketDTO CreateTransferTicket(
        string barcode = "BC123",
        string name = "John Doe",
        string passportNumber = "P1234567",
        string ticketAmount = "25.50",
        int luggageCount = 2,
        string travelDate = "2026-08-15",
        string travelTimeFrom = "14:30:00") => new()
        {
            Status = "success",
            Details = new TicketDetailsWrapper
            {
                Status = "success",
                Data = new TicketDataContainer
                {
                    Name = name,
                    Passport_number = passportNumber,
                    Count_luggage = luggageCount,
                    Ticket_id = "T1",
                    Master_ticket_id = "M1",
                    Ticket_amount = ticketAmount,
                    Barcode = barcode,
                    Ticket_pdf = "pdf",
                    Sent_whatsapp_at = "",
                    Sent_email_at = "",
                    Created_at = "",
                    Updated_at = "",
                    Booking = new BookingData
                    {
                        Payment_ref = "ref",
                        Travel_date = travelDate,
                        Travel_time_from = travelTimeFrom,
                        Travel_time_to = "16:00:00",
                        Booking_date = "2026-08-01",
                        User_email = "user@example.com",
                        User_phone = "+1234567890",
                        User_name = "John Doe",
                        Created_at = "",
                        Updated_at = ""
                    }
                }
            }
        };

    [Test]
    public void Constructor_MapsBarcodeNameAndPassportNumber()
    {
        var transfer = CreateTransferTicket(barcode: "BC999", name: "Jane Smith", passportNumber: "P9999999");

        var result = new VerifiedTicketDTO(transfer);

        result.BarCode.Should().Be("BC999");
        result.OriginalOwnerName.Should().Be("Jane Smith");
        result.OriginalOwnerPassportNumber.Should().Be("P9999999");
        result.Success.Should().BeTrue();
    }

    [Test]
    public void Constructor_CombinesTravelDateAndTime()
    {
        var transfer = CreateTransferTicket(travelDate: "2026-08-15", travelTimeFrom: "14:30:00");

        var result = new VerifiedTicketDTO(transfer);

        result.TicketDateTime.Should().Be(new DateTime(2026, 8, 15, 14, 30, 0));
    }

    [Test]
    public void Constructor_ParsesPriceWithInvariantCulture_AndConvertsJodToUsd()
    {
        var transfer = CreateTransferTicket(ticketAmount: "25.50");

        var result = new VerifiedTicketDTO(transfer);

        result.Price.Should().Be(36.4650m); // 25.50 JOD * 1.43
    }

    [Test]
    public void Constructor_CalculatesTotalPrice_AsConvertedPricePlusTwoPerBag()
    {
        var transfer = CreateTransferTicket(ticketAmount: "20", luggageCount: 3);

        var result = new VerifiedTicketDTO(transfer);

        result.NumberOfBags.Should().Be(3);
        result.TotalPrice.Should().Be(34.60m); // (20 * 1.43) + 3*2
    }

    [Test]
    public void Constructor_TotalPrice_EqualsConvertedPrice_WhenNoLuggage()
    {
        var transfer = CreateTransferTicket(ticketAmount: "20", luggageCount: 0);

        var result = new VerifiedTicketDTO(transfer);

        result.TotalPrice.Should().Be(28.60m); // 20 * 1.43
    }

    [Test]
    public void ParameterlessConstructor_DefaultsSuccessToTrue_AndStringsToEmpty()
    {
        var result = new VerifiedTicketDTO();

        result.Success.Should().BeTrue();
        result.BarCode.Should().BeEmpty();
        result.OriginalOwnerName.Should().BeEmpty();
        result.OriginalOwnerPassportNumber.Should().BeEmpty();
    }
}
