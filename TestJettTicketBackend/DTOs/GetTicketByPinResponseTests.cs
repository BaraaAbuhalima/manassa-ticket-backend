using System.Text.Json;
using jett_exchange_backend.DTOs.Responses;
using FluentAssertions;

namespace TestJettTicketBackend.DTOs;

public class GetTicketByPinResponseTests
{
    [Test]
    public void DeleteToken_IsExcluded_FromJsonSerialization()
    {
        var response = new GetTicketByPinResponse
        {
            Id = Guid.NewGuid(),
            TicketDateTime = DateTime.UtcNow,
            NumberOfBags = 1,
            TotalPrice = 10m,
            DeleteToken = "should-not-appear-in-body"
        };

        var json = JsonSerializer.Serialize(response);

        json.Should().NotContain("should-not-appear-in-body");
        json.Should().NotContain("DeleteToken", "the field name itself should not be serialized either");
    }
}
