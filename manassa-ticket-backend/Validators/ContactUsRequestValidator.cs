using FluentValidation;
using manassa_ticket_backend.Common;
using manassa_ticket_backend.DTOs.Requests;

namespace manassa_ticket_backend.Validators;

public class ContactUsRequestValidator : AbstractValidator<ContactUsRequest>
{
    public ContactUsRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(ValidationConstants.EmailMaxLength)
            .WithMessage($"Email must not exceed {ValidationConstants.EmailMaxLength} characters");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(2000).WithMessage("Message must not exceed 2000 characters");
    }
}
