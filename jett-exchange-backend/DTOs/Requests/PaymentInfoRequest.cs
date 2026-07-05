using System.ComponentModel.DataAnnotations;
using jett_exchange_backend.Common;
using jett_exchange_backend.Common.ValueObjects;

namespace jett_exchange_backend.DTOs.Requests;

public class PaymentInfoRequest
{
    public BankDetails? BankDetails { get; set; }
    [MaxLength(ValidationConstants.PhoneMaxLength)]
    public string? PhoneNumber { get; set; }
}