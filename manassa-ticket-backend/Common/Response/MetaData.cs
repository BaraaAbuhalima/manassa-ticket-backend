namespace manassa_ticket_backend.Common;

public class MetaData
{
    public DateTime Timestamp { get; set; }
    public string? RequestId { get; set; }

    // for lists
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public int? TotalCount { get; set; }
}