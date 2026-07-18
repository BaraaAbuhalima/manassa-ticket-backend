using manassa_ticket_backend.Models;

namespace manassa_ticket_backend.Common;

public static class PaymentInfoFormatter
{
    public static string Describe(PaymentInfo paymentInfo) => paymentInfo switch
    {
        BankTransferInfo b => $"IBAN transfer - {b.BankDetails.AccountHolderName}, {b.BankDetails.BankName} ({b.BankDetails.Country}), account {b.BankDetails.AccountNumber}",
        Reflect r => $"Reflect - {r.PhoneNumber}",
        PhoneTransfer p => $"Phone transfer - {p.PhoneNumber}",
        _ => "N/A"
    };
}
