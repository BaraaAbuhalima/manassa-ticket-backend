using manassa_ticket_backend.Common;
using manassa_ticket_backend.Common.ValueObjects;
using manassa_ticket_backend.Models;
using FluentAssertions;

namespace TestManassaTicketBackend.Common;

public class PaymentInfoFormatterTests
{
    [Test]
    public void Describe_FormatsBankTransfer_WithAccountDetails()
    {
        var info = new BankTransferInfo
        {
            BankDetails = new BankDetails
            {
                AccountHolderName = "Jane Doe",
                BankName = "Arab Bank",
                Country = "Jordan",
                AccountNumber = "JO00ARAB1234567890"
            }
        };

        var description = PaymentInfoFormatter.Describe(info);

        description.Should().Be("IBAN transfer - Jane Doe, Arab Bank (Jordan), account JO00ARAB1234567890");
    }

    [Test]
    public void Describe_FormatsReflect_WithPhoneNumber()
    {
        var description = PaymentInfoFormatter.Describe(new Reflect { PhoneNumber = "0791234567" });

        description.Should().Be("Reflect - 0791234567");
    }

    [Test]
    public void Describe_FormatsPhoneTransfer_WithPhoneNumber()
    {
        var description = PaymentInfoFormatter.Describe(new PhoneTransfer { PhoneNumber = "0791234567" });

        description.Should().Be("Phone transfer - 0791234567");
    }
}
