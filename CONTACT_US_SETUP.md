# Contact Us Endpoint

This describes what was added and exactly what you need to review/replace before relying on
this in production.

## What was added

| File | Purpose |
|---|---|
| `jett-exchange-backend/DTOs/Requests/ContactUsRequest.cs` | Request body: `Name`, `Email`, `Message`. |
| `jett-exchange-backend/Validators/ContactUsRequestValidator.cs` | FluentValidation rules (required fields, email format, length limits). |
| `jett-exchange-backend/Configuration/ContactOptions.cs` | Strongly-typed config: `RecipientEmail`. |
| `jett-exchange-backend/appsettings.json` (`"Contact"` section) | Holds `RecipientEmail`, currently `b.k.1baraa@gmail.com`. |
| `jett-exchange-backend/Services/Contact/IContactUsService.cs` + `ContactUsService.cs` | Builds a `SendEmailMessage` and publishes it via the existing `IEmailMessagePublisher`. |
| `jett-exchange-backend/Controllers/ContactController.cs` | `POST api/contact`. |
| `jett-exchange-backend/Program.cs` | Registers `ContactOptions` and `IContactUsService`. |

No changes were needed in `jett-notification-service` — it already has a generic
`SendEmailMessage` → RabbitMQ → SMTP pipeline (originally built for ticket-availability
notifications), and this endpoint just reuses it.

## How the flow works

1. **`POST /api/contact`** — body: `{ "name": "...", "email": "...", "message": "..." }`
2. `ContactController` calls `ContactUsService.SubmitAsync`, which builds a `SendEmailMessage`
   (`To` = `Contact:RecipientEmail`, `Subject` = `"Contact Us message from {Name}"`, `Body` =
   sender name/email + their message) and publishes it via `IEmailMessagePublisher`.
3. `RabbitMqEmailMessagePublisher` puts the message on the `email-notifications` queue
   (`RabbitMq:EmailNotificationQueueName`).
4. `jett-notification-service`'s `EmailNotificationConsumer` picks it up and sends it with
   `SmtpEmailSender`, using the SMTP settings in `jett-notification-service/appsettings.json`.
5. The API responds immediately with a success message — it does not wait for the email to
   actually be delivered, so a submission can return `200` even if SMTP later fails (see
   "Delivery failures are only logged" below).

## What you'll likely want to change

### 1. Recipient email address

`jett-exchange-backend/appsettings.json`:
```json
"Contact": {
  "RecipientEmail": "b.k.1baraa@gmail.com"
}
```
Change this to whatever inbox should receive contact form submissions. For a quick change
without touching config in source control, use `dotnet user-secrets` locally or an environment
variable in production: `Contact__RecipientEmail` (double underscore is ASP.NET Core's
config-section separator) — same pattern already used for `Stripe__SecretKey` etc.

### 2. Actual email delivery (SMTP settings)

`jett-notification-service/appsettings.json`:
```json
"Smtp": {
  "Host": "mailhog",
  "Port": 1025,
  "Username": "",
  "Password": "",
  "EnableSsl": false,
  "FromAddress": "no-reply@jett-exchange.local"
}
```
This currently points at **MailHog** (`compose.yaml`'s `mailhog` service — a fake SMTP server
for local dev that captures mail instead of sending it; view captured mail at
`http://localhost:8025`). Nothing is actually delivered to `b.k.1baraa@gmail.com` until you
point this at a real SMTP provider (e.g. Gmail SMTP with an app password, SendGrid, Postmark,
SES, etc.) and set `Host`/`Port`/`Username`/`Password`/`EnableSsl`/`FromAddress` accordingly.
Prefer `dotnet user-secrets` / environment variables for `Username`/`Password` rather than
committing real SMTP credentials to `appsettings.json`.

### 3. Email content / subject line

`jett-exchange-backend/Services/Contact/ContactUsService.cs` builds the subject and body:
```csharp
Subject = $"Contact Us message from {request.Name}",
Body = $"From: {request.Name} <{request.Email}>\n\n{request.Message}"
```
Edit this directly if you want a different format, an HTML body, a reply-to header set to the
sender's address (currently there is no `ReplyTo` — replies go to `FromAddress`, not the
sender), or to CC/BCC additional addresses.

### 4. Validation rules

`jett-exchange-backend/Validators/ContactUsRequestValidator.cs` — adjust the `MaximumLength`
values (currently `Name` ≤ 100, `Message` ≤ 2000) or add rules (e.g. a minimum message length,
profanity filtering) here.

### 5. Adding more fields (e.g. Subject, Phone)

Add the property to `ContactUsRequest`, add a corresponding rule in
`ContactUsRequestValidator`, and include it in the `Subject`/`Body` construction in
`ContactUsService.SubmitAsync`.

### 6. No spam/abuse protection yet

This is an unauthenticated, public endpoint that triggers an outbound email on every request —
there's currently no rate limiting, CAPTCHA, or honeypot field. Before exposing this publicly,
consider adding:
- Rate limiting (e.g. ASP.NET Core's built-in `RateLimiter` middleware, keyed by IP).
- A CAPTCHA (e.g. hCaptcha/reCAPTCHA) checked in the controller before calling the service.
- A honeypot field in `ContactUsRequest` that real users leave blank, rejected silently if
  filled in.

### 7. Delivery failures are only logged

If RabbitMQ or SMTP is down, `EmailNotificationConsumer` logs the error and requeues the
message (`BasicNackAsync(..., requeue: true, ...)`), but the original API caller already got a
`200` response and has no way to know delivery is delayed/failing. If you need the submitter to
know their message didn't get through, you'd need synchronous SMTP delivery instead of
fire-and-forget queuing, which is a bigger change to the existing pattern (and would slow down
the API response on every submission).

## Testing locally

```bash
docker compose up --build -d
curl -X POST http://localhost:8080/api/contact \
  -H "Content-Type: application/json" \
  -d '{"name":"Test User","email":"test@example.com","message":"Just checking this works."}'
```
Then open `http://localhost:8025` (MailHog UI) to see the captured email instead of it
actually landing in `b.k.1baraa@gmail.com`, until real SMTP credentials are configured per
section 2 above.
