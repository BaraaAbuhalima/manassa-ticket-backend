using FluentValidation;
using manassa_ticket_backend.Common;
using manassa_ticket_backend.DTOs.Requests;

namespace manassa_ticket_backend.Validators;

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
            .MaximumLength(ValidationConstants.EmailMaxLength)
            .WithMessage($"Buyer email must not exceed {ValidationConstants.EmailMaxLength} characters");
    }
}
