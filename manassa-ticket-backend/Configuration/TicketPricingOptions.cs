namespace manassa_ticket_backend.Configuration;

public class TicketPricingOptions
{
    // The most a seller's asking price may exceed the original ticket's printed price,
    // enforced both at listing time (TicketProcessor) and on later price edits (TicketDeleter).
    public decimal MaxAskingPriceIncreaseJod { get; set; } = 1m;
}