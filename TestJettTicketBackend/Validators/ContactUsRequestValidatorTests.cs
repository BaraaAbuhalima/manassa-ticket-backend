using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Validators;
using FluentAssertions;

namespace TestJettTicketBackend.Validators;

public class ContactUsRequestValidatorTests
{
    private ContactUsRequestValidator _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new ContactUsRequestValidator();

    private static ContactUsRequest CreateRequest(
        string name = "Jane Doe",
        string email = "jane@example.com",
        string message = "Hello, I have a question.") => new()
        {
            Name = name,
            Email = email,
            Message = message
        };

    [Test]
    public void Validate_Passes_ForValidRequest()
    {
        var result = _sut.Validate(CreateRequest());

        result.IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase(" ")]
    public void Validate_Fails_ForMissingName(string name)
    {
        var result = _sut.Validate(CreateRequest(name: name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Test]
    public void Validate_Fails_WhenNameExceedsMaxLength()
    {
        var result = _sut.Validate(CreateRequest(name: new string('a', 101)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [TestCase("")]
    [TestCase("not-an-email")]
    public void Validate_Fails_ForInvalidEmail(string email)
    {
        var result = _sut.Validate(CreateRequest(email: email));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [TestCase("")]
    [TestCase(" ")]
    public void Validate_Fails_ForMissingMessage(string message)
    {
        var result = _sut.Validate(CreateRequest(message: message));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Message");
    }

    [Test]
    public void Validate_Fails_WhenMessageExceedsMaxLength()
    {
        var result = _sut.Validate(CreateRequest(message: new string('a', 2001)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Message");
    }
}
