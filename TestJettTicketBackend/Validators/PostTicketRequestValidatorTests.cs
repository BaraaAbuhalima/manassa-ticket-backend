using jett_exchange_backend.Common.ValueObjects;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Models;
using jett_exchange_backend.Validators;
using FluentAssertions;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Validators;

public class PostTicketRequestValidatorTests
{
    private PostTicketRequestValidator _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new PostTicketRequestValidator();

    private static PostTicketRequest ValidRequest() => new()
    {
        File = FakeFormFile.CreatePdf(),
        SellerEmail = "seller@example.com",
        SellerPhone = "+1234567890",
        Price = 10m,
        PaymentMethod = PaymentMethod.Reflect,
        PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567" },
        SellerName = "Seller"
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
        request.PaymentMethod = PaymentMethod.Iban;
        request.PaymentInfoRequest = new PaymentInfoRequest
        {
            BankDetails = new BankDetails { AccountNumber = "1", BankName = "B", Country = "JO", AccountHolderName = "A" }
        };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenFileIsEmpty()
    {
        var request = ValidRequest();
        request.File = FakeFormFile.CreatePdf(content: []);

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "File");
    }

    [Test]
    public void Validate_Fails_WhenFileIsNotPdf()
    {
        var request = ValidRequest();
        request.File = FakeFormFile.CreatePdf(contentType: "image/png");

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "File");
    }

    [Test]
    public void Validate_Fails_WhenPriceIsZeroOrNegative()
    {
        var request = ValidRequest();
        request.Price = 0m;

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Price");
    }

    [Test]
    public void Validate_Fails_WhenSellerEmailIsInvalid()
    {
        var request = ValidRequest();
        request.SellerEmail = "not-an-email";

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SellerEmail");
    }

    [Test]
    public void Validate_Fails_WhenSellerPhoneIsInvalid()
    {
        var request = ValidRequest();
        request.SellerPhone = "not-a-phone-number!!";

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SellerPhone");
    }

    [Test]
    public void Validate_Fails_WhenPaymentInfoRequestIsNull()
    {
        // Simulates what real [FromForm] model binding produces when the client
        // omits the field entirely: "required" is compile-time only, so the
        // property can still be null at runtime.
        var request = ValidRequest();
        request.PaymentInfoRequest = null!;

        var act = () => _sut.Validate(request);

        act.Should().NotThrow();
        var result = act();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PaymentInfoRequest");
    }

    [Test]
    public void Validate_Fails_WhenIbanAndBankDetailsIsNull()
    {
        var request = ValidRequest();
        request.PaymentMethod = PaymentMethod.Iban;
        request.PaymentInfoRequest = new PaymentInfoRequest();

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PaymentInfoRequest.BankDetails");
    }

    [TestCase("", "B", "JO", "A")]
    [TestCase("1", "", "JO", "A")]
    [TestCase("1", "B", "", "A")]
    [TestCase("1", "B", "JO", "")]
    public void Validate_Fails_WhenIbanAndBankDetailsIsIncomplete(
        string accountNumber, string bankName, string country, string accountHolderName)
    {
        var request = ValidRequest();
        request.PaymentMethod = PaymentMethod.Iban;
        request.PaymentInfoRequest = new PaymentInfoRequest
        {
            BankDetails = new BankDetails
            {
                AccountNumber = accountNumber,
                BankName = bankName,
                Country = country,
                AccountHolderName = accountHolderName
            }
        };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PaymentInfoRequest.BankDetails");
    }

    [TestCase(PaymentMethod.Reflect)]
    [TestCase(PaymentMethod.Phone)]
    public void Validate_Fails_WhenReflectOrPhoneAndPhoneNumberMissing(PaymentMethod method)
    {
        var request = ValidRequest();
        request.PaymentMethod = method;
        request.PaymentInfoRequest = new PaymentInfoRequest();

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PaymentInfoRequest.PhoneNumber");
    }

    [Test]
    public void Validate_IgnoresBankDetails_WhenMethodIsReflect()
    {
        var request = ValidRequest();
        request.PaymentMethod = PaymentMethod.Reflect;
        request.PaymentInfoRequest = new PaymentInfoRequest { PhoneNumber = "0791234567", BankDetails = null };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}