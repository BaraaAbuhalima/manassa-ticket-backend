using FluentValidation;
using jett_exchange_backend.Common.ValueObjects;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Models;

namespace jett_exchange_backend.Validators;

public class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Payment is not null || x.Price is not null)
            .WithName("Request")
            .WithMessage("At least one field must be provided to update");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than zero");

        When(x => x.Payment is not null, () =>
        {
            RuleFor(x => x.Payment!.PaymentInfoRequest)
                .Custom(ValidatePaymentInfo);
        });
    }

    private static void ValidatePaymentInfo(PaymentInfoRequest? paymentInfoRequest, ValidationContext<UpdateTicketRequest> context)
    {
        if (paymentInfoRequest is null)
        {
            context.AddFailure("Payment.PaymentInfoRequest", "Payment info is required");
            return;
        }

        switch (context.InstanceToValidate.Payment!.PaymentMethod)
        {
            case PaymentMethod.Iban when !HaveAllBankDetailFields(paymentInfoRequest.BankDetails):
                context.AddFailure("Payment.PaymentInfoRequest.BankDetails",
                    "Bank details must include account number, bank name, country, and account holder name");
                break;
            case PaymentMethod.Reflect or PaymentMethod.Phone when string.IsNullOrWhiteSpace(paymentInfoRequest.PhoneNumber):
                context.AddFailure("Payment.PaymentInfoRequest.PhoneNumber",
                    "Phone number is required for this payment method");
                break;
        }
    }

    private static bool HaveAllBankDetailFields(BankDetails? bankDetails)
    {
        return bankDetails is not null
               && !string.IsNullOrWhiteSpace(bankDetails.AccountNumber)
               && !string.IsNullOrWhiteSpace(bankDetails.BankName)
               && !string.IsNullOrWhiteSpace(bankDetails.Country)
               && !string.IsNullOrWhiteSpace(bankDetails.AccountHolderName);
    }
}
