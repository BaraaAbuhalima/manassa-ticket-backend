using manassa_ticket_backend.Common;
using FluentAssertions;

namespace TestManassaTicketBackend.Common;

public class FeeCalculatorTests
{
    [Test]
    public void CalculateFeeUsd_AddsFlatFeeAndPercentOfPrice()
    {
        // 1 flat + 5% of 50 = 1 + 2.50 = 3.50
        var fee = FeeCalculator.CalculateFeeUsd(ticketPriceUsd: 50m, flatFeeUsd: 1m, percentFee: 5m);

        fee.Should().Be(3.50m);
    }

    [Test]
    public void CalculateFeeUsd_EqualsFlatFee_WhenPercentIsZero()
    {
        var fee = FeeCalculator.CalculateFeeUsd(ticketPriceUsd: 50m, flatFeeUsd: 1m, percentFee: 0m);

        fee.Should().Be(1m);
    }

    [Test]
    public void CalculateFeeUsd_EqualsPercentOfPrice_WhenFlatFeeIsZero()
    {
        var fee = FeeCalculator.CalculateFeeUsd(ticketPriceUsd: 50m, flatFeeUsd: 0m, percentFee: 5m);

        fee.Should().Be(2.50m);
    }

    [Test]
    public void CalculateFeeUsd_RoundsToTwoDecimalPlaces()
    {
        // 1 + 5% of 33.33 = 1 + 1.6665 -> rounds to 2.67 (away from zero)
        var fee = FeeCalculator.CalculateFeeUsd(ticketPriceUsd: 33.33m, flatFeeUsd: 1m, percentFee: 5m);

        fee.Should().Be(2.67m);
    }

    [Test]
    public void CalculateFeeUsd_IsZero_WhenBothComponentsAreZero()
    {
        var fee = FeeCalculator.CalculateFeeUsd(ticketPriceUsd: 50m, flatFeeUsd: 0m, percentFee: 0m);

        fee.Should().Be(0m);
    }
}
