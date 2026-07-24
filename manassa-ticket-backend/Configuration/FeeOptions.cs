namespace manassa_ticket_backend.Configuration;

public class FeeOptions
{
    public decimal FlatFeeUsd { get; set; } = 1m;

    // A percentage point value, not a fraction — 5 means 5%, not 500%. Applied as
    // TotalPriceUsd * (PercentFee / 100) when computing the buyer-facing service fee.
    public decimal PercentFee { get; set; } = 5m;
}