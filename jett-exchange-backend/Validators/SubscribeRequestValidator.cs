using FluentValidation;
using jett_exchange_backend.DTOs.Requests;

namespace jett_exchange_backend.Validators;

public class SubscribeRequestValidator : AbstractValidator<SubscribeRequest>
{
    public SubscribeRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Date)
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date must not be in the past");
    }
}
