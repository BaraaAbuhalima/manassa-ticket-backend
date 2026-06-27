using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace jett_exchange_backend.Common.ValueObjects;

[Owned]
public class BankDetails
{
    [MaxLength(30)]
    public string AccountNumber { get; set; }
    [MaxLength(40)]
    public string BankName { get; set; }
    [MaxLength(30)]
    public string Country { get; set; }
    [MaxLength(40)]
    public string AccountHolderName { get; set; }
}