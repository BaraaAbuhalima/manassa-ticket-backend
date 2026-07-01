# Connecting Stripe to the Ticket Purchase Endpoint

This describes what was added and exactly what you need to replace to go live with real
Stripe payments.

## What was added

| File | Purpose |
|---|---|
| `jett-exchange-backend/Configuration/StripeOptions.cs` | Strongly-typed config: `SecretKey`, `PublishableKey`, `WebhookSecret`, `Currency`. |
| `jett-exchange-backend/appsettings.json` (`"Stripe"` section) | Holds the values above. **Placeholders — must be replaced.** |
| `jett-exchange-backend/Program.cs` | Registers `StripeOptions`, sets `Stripe.StripeConfiguration.ApiKey`, registers `ITicketPurchaseService`. |
| `jett-exchange-backend/Models/Ticket.cs` | Added `BuyerName`, `BuyerEmail`, `StripePaymentIntentId` fields. |
| `jett-exchange-backend/DTOs/Requests/PurchaseTicketRequest.cs` | Request body: `BuyerName`, `BuyerEmail`. |
| `jett-exchange-backend/DTOs/Responses/PurchaseTicketResponse.cs` | Response: `TicketId`, `ClientSecret`, `PublishableKey`, `Amount`, `Currency`. |
| `jett-exchange-backend/Validators/PurchaseTicketRequestValidator.cs` | FluentValidation rules for the request. |
| `jett-exchange-backend/Services/Payments/ITicketPurchaseService.cs` + `TicketPurchaseService.cs` | Creates the Stripe `PaymentIntent` and handles the webhook that marks a ticket `Sold`. |
| `jett-exchange-backend/Controllers/PaymentController.cs` | `POST api/payment/ticket/{id}` and `POST api/payment/webhook`. |

## How the flow works

Stripe payments are two-step by design — the server never just "charges a card" directly,
because the actual card confirmation happens on the client (or via Stripe Checkout), and the
server only finds out it truly succeeded via a signed webhook. This repo follows that pattern:

1. **`POST /api/payment/ticket/{id}`** — body: `{ "buyerName": "...", "buyerEmail": "..." }`
   - Looks up the ticket by id, checks it's still `ForSale`.
   - Creates a Stripe `PaymentIntent` for `ticket.TotalPrice` in the configured `Currency`.
   - Stores `StripePaymentIntentId`, `BuyerName`, `BuyerEmail` on the ticket (ticket stays
     `ForSale` at this point — it is **not** marked sold yet).
   - Returns `clientSecret` + `publishableKey` so your frontend can call
     `stripe.confirmPayment(...)` (Stripe.js) to complete the charge.
2. **`POST /api/payment/webhook`** — Stripe calls this itself when the payment intent
   resolves. On `payment_intent.succeeded`, the matching ticket is transitioned to `Sold`.

**Do not** mark a ticket `Sold` directly from the create-intent call or trust a client-side
"payment succeeded" flag — only the webhook (verified via `WebhookSecret`) should flip the
ticket to `Sold`, otherwise a buyer could claim a ticket without ever paying.

## What you must replace before this works

### 1. Stripe API keys (`jett-exchange-backend/appsettings.json`)

```json
"Stripe": {
  "SecretKey": "sk_test_REPLACE_WITH_YOUR_STRIPE_SECRET_KEY",
  "PublishableKey": "pk_test_REPLACE_WITH_YOUR_STRIPE_PUBLISHABLE_KEY",
  "WebhookSecret": "whsec_REPLACE_WITH_YOUR_STRIPE_WEBHOOK_SECRET",
  "Currency": "usd"
}
```

- Get `SecretKey` / `PublishableKey` from the Stripe Dashboard → **Developers → API keys**.
  Use the **test mode** keys (`sk_test_...` / `pk_test_...`) until you're ready to go live,
  then swap to the live keys (`sk_live_...` / `pk_live_...`).
- `Currency` must be a Stripe-supported 3-letter ISO currency code, lowercase (e.g. `"usd"`,
  `"jod"`, `"eur"`). Check that your Stripe account supports it — not all currencies are
  enabled by default for every account/country.
- **Do not commit real keys to git.** For local dev, prefer `dotnet user-secrets` or an
  untracked `appsettings.Development.json` / environment variables instead of editing
  `appsettings.json` directly:
  ```bash
  cd jett-exchange-backend
  dotnet user-secrets init
  dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
  dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
  dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
  ```
  In production (see `compose.yaml`), set these as environment variables instead:
  `Stripe__SecretKey`, `Stripe__PublishableKey`, `Stripe__WebhookSecret` (double underscore
  is ASP.NET Core's config-section separator).

### 2. Webhook endpoint registration (Stripe Dashboard)

1. Go to **Developers → Webhooks → Add endpoint**.
2. Endpoint URL: `https://<your-public-domain>/api/payment/webhook`
   (must be publicly reachable over HTTPS — `localhost` won't work directly; see the Stripe
   CLI note below for local testing).
3. Select the event: **`payment_intent.succeeded`** (optionally also
   `payment_intent.payment_failed` if you want to add failure handling later).
4. After creating it, Stripe shows a **Signing secret** starting with `whsec_...` — put that
   in `Stripe:WebhookSecret` above. This is what `EventUtility.ConstructEvent(...)` in
   `TicketPurchaseService.HandleStripeWebhookAsync` uses to verify the request really came
   from Stripe.

### 3. Local webhook testing (no public domain yet)

Use the [Stripe CLI](https://stripe.com/docs/stripe-cli) to forward events to your local
server instead of registering a public endpoint:

```bash
stripe login
stripe listen --forward-to https://localhost:<port>/api/payment/webhook
```

`stripe listen` prints a temporary `whsec_...` signing secret — use that value for
`Stripe:WebhookSecret` while testing locally.

### 4. Test the purchase flow with Stripe test cards

Once keys are set, call:

```bash
curl -X POST https://localhost:<port>/api/payment/ticket/<ticketId> \
  -H "Content-Type: application/json" \
  -d '{"buyerName":"Test Buyer","buyerEmail":"buyer@example.com"}'
```

Take the returned `clientSecret` and confirm it from a frontend using Stripe.js/Elements
with a [Stripe test card](https://stripe.com/docs/testing) (e.g. `4242 4242 4242 4242`, any
future expiry, any CVC). After confirmation succeeds, Stripe fires the webhook and the
ticket's `Status` flips to `Sold` — verify with `GET /api/ticket/{id}`.

### 5. Currency/amount note

`Ticket.TotalPrice` is currently mapped as `decimal(3,2)` in
`jett-exchange-backend/Models/Ticket.cs:27`, which only allows values up to `9.99`. Stripe
amounts are sent in the currency's smallest unit (e.g. cents for USD), computed in
`TicketPurchaseService.CreatePaymentIntentAsync` as
`(long)Math.Round(ticket.TotalPrice * 100, ...)`. If real ticket prices exceed `9.99`, you'll
need to widen that column type (e.g. `decimal(10,2)`) — this is a pre-existing issue in the
codebase, not something introduced by the payment feature, but it will silently truncate
real-world ticket prices if left as-is.

### 6. Production database note

The app currently runs on EF Core's **in-memory database**
(`Program.cs`: `options.UseInMemoryDatabase("JettTickets")`), so `StripePaymentIntentId` /
`BuyerName` / `BuyerEmail` are not durably persisted across restarts. This is unrelated to
Stripe itself but worth knowing before relying on this in a real deployment — switch to a
real EF Core provider (e.g. `Npgsql`/`SqlServer`) when you're ready.
