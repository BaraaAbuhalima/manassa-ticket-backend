using FluentValidation;
using jett_exchange_backend.DTOs.Requests;

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

        RuleFor(x => x.Email).NotNull().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .Matches(@"^\+?(\d{1,3})?[-.\s]?\(?\d{1,4}?\)?[-.\s]?\d{1,4}[-.\s]?\d{1,9}$")
            .WithMessage("Invalid phone number format");
    }
}