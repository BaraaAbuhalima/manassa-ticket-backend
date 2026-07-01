using System.Text.Json;
using System.Text.Json.Serialization;
using jett_exchange_backend.Common.ValueObjects;

namespace jett_exchange_backend.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BankTransferInfo), "bankTransfer")]
[JsonDerivedType(typeof(Reflect), "reflect")]
[JsonDerivedType(typeof(PhoneTransfer), "phoneTransfer")]
public abstract class PaymentInfo
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

public class BankTransferInfo : PaymentInfo
{
    public required BankDetails BankDetails { get; set; }
}

public class Reflect : PaymentInfo
{
    public required string PhoneNumber { get; set; }
}

public class PhoneTransfer : PaymentInfo
{
    public required string PhoneNumber { get; set; }
}