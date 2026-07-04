using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Models;

namespace jett_exchange_backend.Services.Tickets;

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
