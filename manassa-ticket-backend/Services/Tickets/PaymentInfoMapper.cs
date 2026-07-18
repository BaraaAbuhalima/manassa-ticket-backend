using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Models;

namespace manassa_ticket_backend.Services.Tickets;

public static class PaymentInfoMapper
{
    public static PaymentInfo Build(PaymentMethod paymentMethod, PaymentInfoRequest paymentInfoRequest) =>
        paymentMethod switch
        {
            PaymentMethod.Iban => new BankTransferInfo
            {
                BankDetails = paymentInfoRequest.BankDetails!
            },
            PaymentMethod.Reflect => new Reflect
            {
                PhoneNumber = paymentInfoRequest.PhoneNumber!
            },
            PaymentMethod.Phone => new PhoneTransfer
            {
                PhoneNumber = paymentInfoRequest.PhoneNumber!
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(paymentMethod), paymentMethod, "Unsupported payment method")
        };
}
