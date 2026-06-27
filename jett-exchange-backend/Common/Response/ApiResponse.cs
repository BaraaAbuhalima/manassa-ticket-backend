namespace jett_exchange_backend.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }

    public T? Data { get; set; }

    public List<string>? Errors { get; set; }

    public MetaData? Meta { get; set; }

    public Dictionary<string, string>? Links { get; set; }
}
