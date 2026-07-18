using manassa_ticket_backend.Common.ValueObjects;
using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Models;
using manassa_ticket_backend.Validators;
using FluentAssertions;

namespace TestManassaTicketBackend.Validators;

public class UpdateTicketRequestValidatorTests
{
    private UpdateTicketRequestValidator _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new UpdateTicketRequestValidator();

    private static UpdateTicketRequest ValidRequest() => new()
    {
        Payment = new PaymentUpdateRequest
        {
            PaymentMethod = PaymentMethod.Reflect,
            PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567" }
        }
    };

    [Test]
    public void Validate_Passes_ForFullyValidReflectRequest()
    {
        var result = _sut.Validate(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Passes_ForFullyValidIbanRequest()
    {
        var request = ValidRequest();
        request.Payment!.PaymentMethod = PaymentMethod.Iban;
        request.Payment.PaymentInfoRequest = new PaymentInfoRequest
        {
            BankDetails = new BankDetails { AccountNumber = "1", BankName = "B", Country = "JO", AccountHolderName = "A" }
        };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenNoFieldsProvided()
    {
        var result = _sut.Validate(new UpdateTicketRequest());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Request");
    }

    [Test]
    public void Validate_Passes_ForPriceOnlyRequest()
    {
        var result = _sut.Validate(new UpdateTicketRequest { Price = 15m });

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenPriceIsZeroOrNegative()
    {
        var result = _sut.Validate(new UpdateTicketRequest { Price = 0m });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Price");
    }

    [Test]
    public void Validate_Fails_WhenPaymentInfoRequestIsNull()
    {
        var request = ValidRequest();
        request.Payment!.PaymentInfoRequest = null!;

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Payment.PaymentInfoRequest");
    }

    [Test]
    public void Validate_Fails_WhenIbanAndBankDetailsIsNull()
    {
        var request = ValidRequest();
        request.Payment!.PaymentMethod = PaymentMethod.Iban;
        request.Payment.PaymentInfoRequest = new PaymentInfoRequest();

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Payment.PaymentInfoRequest.BankDetails");
    }

    [TestCase(PaymentMethod.Reflect)]
    [TestCase(PaymentMethod.Phone)]
    public void Validate_Fails_WhenReflectOrPhoneAndPhoneNumberMissing(PaymentMethod method)
    {
        var request = ValidRequest();
        request.Payment!.PaymentMethod = method;
        request.Payment.PaymentInfoRequest = new PaymentInfoRequest();

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Payment.PaymentInfoRequest.PhoneNumber");
    }
}
