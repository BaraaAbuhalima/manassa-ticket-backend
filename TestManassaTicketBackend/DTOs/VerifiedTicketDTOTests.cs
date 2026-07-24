using manassa_ticket_backend.DTOs.TicketVerification;
using FluentAssertions;

namespace TestManassaTicketBackend.DTOs;

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
            Details = new TicketDetailsWrapper
            {
                Data = new TicketDataContainer
                {
                    Name = name,
                    Passport_number = passportNumber,
                    Count_luggage = luggageCount,
                    Ticket_amount = ticketAmount,
                    Barcode = barcode,
                    Booking = new BookingData
                    {
                        Travel_date = travelDate,
                        Travel_time_from = travelTimeFrom
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
    public void Constructor_CalculatesTotalPriceJod_AsRawAmountPlusTwoPerBag()
    {
        var transfer = CreateTransferTicket(ticketAmount: "20", luggageCount: 3);

        var result = new VerifiedTicketDTO(transfer);

        result.NumberOfBags.Should().Be(3);
        result.TotalPriceJod.Should().Be(26m); // 20 JOD + 3 bags * 2 JOD
    }

    [Test]
    public void Constructor_TotalPriceJod_EqualsRawAmount_WhenNoLuggage()
    {
        var transfer = CreateTransferTicket(ticketAmount: "20", luggageCount: 0);

        var result = new VerifiedTicketDTO(transfer);

        result.TotalPriceJod.Should().Be(20m);
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
