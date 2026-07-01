using System.ComponentModel.DataAnnotations;

namespace jett_exchange_backend.Common.ValueObjects;

public class BankDetails
{
    [MaxLength(30)]
    public required string AccountNumber { get; set; }
    [MaxLength(40)]
    public required string BankName { get; set; }
    [MaxLength(30)]
    public required string Country { get; set; }
    [MaxLength(40)]
    public required string AccountHolderName { get; set; }
}