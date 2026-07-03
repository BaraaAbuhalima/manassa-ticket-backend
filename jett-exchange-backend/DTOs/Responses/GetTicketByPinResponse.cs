using System.Text.Json.Serialization;

namespace jett_exchange_backend.DTOs.Responses;

public class GetTicketByPinResponse
{
    public required Guid Id { get; set; }
    public required DateTime TicketDateTime { get; set; }
    public required int NumberOfBags { get; set; }
    public required decimal TotalPrice { get; set; }

    // Returned to the client via the X-Delete-Token response header (see
    // TicketController.GetByPin), not the JSON body. Not `required`: System.Text.Json
    // rejects combining `required` with [JsonIgnore] since it could never be satisfied
    // on deserialization.
    [JsonIgnore]
    public string DeleteToken { get; set; } = string.Empty;
}
