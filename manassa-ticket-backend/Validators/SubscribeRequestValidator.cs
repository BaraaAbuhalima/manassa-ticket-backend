using FluentValidation;
using manassa_ticket_backend.Common;
using manassa_ticket_backend.DTOs.Requests;

namespace manassa_ticket_backend.Validators;

public class SubscribeRequestValidator : AbstractValidator<SubscribeRequest>
{
    public SubscribeRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(ValidationConstants.EmailMaxLength)
            .WithMessage($"Email must not exceed {ValidationConstants.EmailMaxLength} characters");

        RuleFor(x => x.Date)
            .GreaterThanOrEqualTo(_ => Clock.TodayForSearch())
            .WithMessage("Date must not be in the past");
    }
}
