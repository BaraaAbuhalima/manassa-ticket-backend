using FluentValidation;
using jett_exchange_backend.DTOs.Requests;

namespace jett_exchange_backend.Validators;

public class PurchaseTicketRequestValidator : AbstractValidator<PurchaseTicketRequest>
{
    public PurchaseTicketRequestValidator()
    {
        RuleFor(x => x.BuyerName)
            .NotEmpty().WithMessage("Buyer name is required")
            .MaximumLength(50).WithMessage("Buyer name must not exceed 50 characters");

        RuleFor(x => x.BuyerEmail)
            .NotEmpty().WithMessage("Buyer email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(254).WithMessage("Buyer email must not exceed 254 characters");
    }
}
