using FluentValidation;
using jett_exchange_backend.Common.ValueObjects;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Models;

namespace jett_exchange_backend.Validators;


public class PostTicketRequestValidator : AbstractValidator<PostTicketRequest>
{
    public PostTicketRequestValidator()
    {
        RuleFor(x => x.File)
            .NotEmpty().WithMessage("PDF file is required")
            .Must(file => file != null && file.Length > 0)
            .WithMessage("File cannot be empty")
            .Must(file => file == null || file.ContentType == "application/pdf")
            .WithMessage("Only PDF files are allowed");

        RuleFor(x => x.Price)
            .NotEmpty().WithMessage("Price is required")
            .GreaterThan(0).WithMessage("Price must be a positive value");

        RuleFor(x => x.SellerEmail).NotNull().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.SellerPhone)
            .NotEmpty().WithMessage("Phone number is required")
            .Matches(@"^\+?(\d{1,3})?[-.\s]?\(?\d{1,4}?\)?[-.\s]?\d{1,4}[-.\s]?\d{1,9}$")
            .WithMessage("Invalid phone number format");

        RuleFor(x => x.PaymentInfoRequest)
            .Custom(ValidatePaymentInfo);
    }

    private static void ValidatePaymentInfo(PaymentInfoRequest? paymentInfoRequest, ValidationContext<PostTicketRequest> context)
    {
        if (paymentInfoRequest is null)
        {
            context.AddFailure("PaymentInfoRequest", "Payment info is required");
            return;
        }

        switch (context.InstanceToValidate.PaymentMethod)
        {
            case PaymentMethod.Iban when !HaveAllBankDetailFields(paymentInfoRequest.BankDetails):
                context.AddFailure("PaymentInfoRequest.BankDetails",
                    "Bank details must include account number, bank name, country, and account holder name");
                break;
            case PaymentMethod.Reflect or PaymentMethod.Phone when string.IsNullOrWhiteSpace(paymentInfoRequest.PhoneNumber):
                context.AddFailure("PaymentInfoRequest.PhoneNumber",
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