namespace manassa_ticket_backend.Common;

// Search/subscription date bounds are anchored to UTC+10 rather than the server's own
// clock, so "today" for date-search purposes can already have rolled over even while it's
// still yesterday evening UTC.
public static class Clock
{
    private static readonly TimeSpan SearchTimeZoneOffset = TimeSpan.FromHours(10);

    public static DateOnly TodayForSearch() => DateOnly.FromDateTime(DateTime.UtcNow + SearchTimeZoneOffset);
}