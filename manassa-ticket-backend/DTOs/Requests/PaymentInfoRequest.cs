using System.ComponentModel.DataAnnotations;
using manassa_ticket_backend.Common;
using manassa_ticket_backend.Common.ValueObjects;

namespace manassa_ticket_backend.DTOs.Requests;

public class PaymentInfoRequest
{
    public BankDetails? BankDetails { get; set; }
    [MaxLength(ValidationConstants.PhoneMaxLength)]
    public string? PhoneNumber { get; set; }
}