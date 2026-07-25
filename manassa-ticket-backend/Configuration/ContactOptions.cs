namespace manassa_ticket_backend.Configuration;

public class ContactOptions
{
    public string RecipientEmail { get; set; } = string.Empty;
    // Credentials for the support mailbox itself (a separate SMTP login from Smtp:Username),
    // used to authenticate when sending the Contact Us confirmation from RecipientEmail.
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
}
