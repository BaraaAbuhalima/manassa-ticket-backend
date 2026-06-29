using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using jett_exchange_backend.Common.ValueObjects;

namespace jett_exchange_backend.Models;

public abstract class PaymentInfo
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
}


public class BankTransferInfo : PaymentInfo
{
    public required BankDetails BankDetails { get; set; }
}
public class Reflect : PaymentInfo
{
    [MaxLength(20)]
    public required string PhoneNumber { get; set; }
}
public class PhoneTransfer : PaymentInfo
{
    [MaxLength(20)]
    public required string PhoneNumber { get; set; }
}
