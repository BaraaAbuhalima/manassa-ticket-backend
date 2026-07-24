namespace manassa_ticket_backend.Common;

public static class CurrencyConversion
{
    public const decimal JodToUsdRate = 1.43m;
    public const decimal UsdToJodRate = 0.68m;

    public static decimal JodToUsd(decimal jod) =>
        Math.Round(jod * JodToUsdRate, 2, MidpointRounding.AwayFromZero);
}
