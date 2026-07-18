namespace manassa_ticket_backend.Common;

public static class FeeCalculator
{
    // percentFee is a percentage point value (5 means 5%), not a fraction.
    public static decimal CalculateFeeUsd(decimal ticketPriceUsd, decimal flatFeeUsd, decimal percentFee) =>
        Math.Round(flatFeeUsd + (ticketPriceUsd * percentFee / 100m), 2, MidpointRounding.AwayFromZero);
}
