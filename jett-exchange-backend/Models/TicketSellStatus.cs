namespace jett_exchange_backend.Models;

public enum TicketSellStatus
{
    Deleted,
    ForSale,
    Sold,
    Processing,
    Rejected,
    // Appended rather than inserted: Status is stored as a plain integer in Postgres,
    // so reordering existing members would silently reinterpret every stored row.
    Reserved
}