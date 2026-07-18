using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Validators;
using FluentAssertions;

namespace TestManassaTicketBackend.Validators;

public class SubscribeRequestValidatorTests
{
    private SubscribeRequestValidator _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new SubscribeRequestValidator();

    [Test]
    public void Validate_Passes_ForValidEmailAndFutureDate()
    {
        var request = new SubscribeRequest { Email = "user@example.com", Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5) };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Passes_ForTodaysDate()
    {
        var request = new SubscribeRequest { Email = "user@example.com", Date = DateOnly.FromDateTime(DateTime.UtcNow) };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("not-an-email")]
    public void Validate_Fails_ForInvalidEmail(string email)
    {
        var request = new SubscribeRequest { Email = email, Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Test]
    public void Validate_Fails_ForPastDate()
    {
        var request = new SubscribeRequest { Email = "user@example.com", Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Date");
    }
}
