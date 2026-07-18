# RabbitMQ messaging

How `manassa-ticket-backend` uses RabbitMQ to decouple ticket posting, ticket-availability
notifications, and email sending from the request path.

## Why it's here

Several things need to happen without blocking an HTTP response:

- When a ticket becomes available again, everyone subscribed to that date needs an email.
- Contact-us submissions need to be emailed to the site owner.

Posting a new ticket (download from R2, extract, verify) is deliberately *not* on this list —
see [R2_STORAGE.md](R2_STORAGE.md) for why that path blocks the request instead.

All of these are pushed onto RabbitMQ queues instead of being done inline, so a slow/broken
extraction service or SMTP server can never fail (or hang) the request that triggered them. All
of this — publishers, consumers, and the SMTP send itself — lives in this one ASP.NET Core
process; there is no separate worker service.

## Topology

Four durable queues, all bound to the default (nameless) exchange and published to by
routing key = queue name — no exchange/routing-key setup, just `queue.declare` + `basic.publish`.
All are published *and* consumed by the same process:

| Queue | Purpose |
|---|---|
| `ticket-available` | A ticket became available on a given date |
| `email-notifications` | A generic email needs to be sent (contact-us, ticket-available) |
| `ticket-sold-notifications` | A ticket sold — notify the seller |
| `ticket-purchased-notifications` | A ticket sold — email the buyer their ticket |

The seller/buyer sold-notification emails get their own dedicated queues rather than sharing
`email-notifications`, so a backlog on one (e.g. a burst of contact-us spam) can't delay the
other, and each can be reasoned about/monitored independently in the management UI.

Queue names are configured, not hardcoded — see [Configuration](#configuration).

## Message flow

Posting a ticket happens entirely inline on the request, ahead of everything below — see
[R2_STORAGE.md](R2_STORAGE.md) for the full upload flow. In short: `TicketPoster` calls
`TicketProcessor` directly (no queue hop), which downloads the PDF from R2, extracts/verifies
it, and either persists the listing (`Status = ForSale`) — nothing is written to the database
before this succeeds — or returns a rejection reason with nothing persisted, having already
deleted the file from R2. Finalizing a listing is what triggers the `ticket-available` publish
below.

```mermaid
sequenceDiagram
    participant Poster as TicketProcessor / TicketDeleter
    participant TAQ as queue: ticket-available
    participant Consumer as TicketAvailableConsumer
    participant Notifier as TicketAvailableNotifier
    participant DB as AppDbContext
    participant ENQ as queue: email-notifications
    participant EConsumer as EmailNotificationConsumer
    participant SMTP as SmtpEmailSender

    Poster->>TAQ: publish TicketAvailableMessage {TicketId, Date}
    TAQ->>Consumer: deliver
    Consumer->>Notifier: NotifySubscribersAsync(message)
    Notifier->>DB: find subscriptions for Date where !Notified
    loop each matching subscriber
        Notifier->>ENQ: publish SendEmailMessage {To, Subject, Body}
        Notifier->>DB: mark subscription.Notified = true
    end
    ENQ->>EConsumer: deliver
    EConsumer->>SMTP: SendAsync(To, Subject, Body)
```

`ContactUsService` takes a shortcut: it publishes a `SendEmailMessage` straight onto
`email-notifications`, skipping the `ticket-available` hop entirely.

`TicketPurchaseService`'s Stripe webhook handler does the same kind of direct publish once a
payment succeeds, but onto its own two queues instead of `email-notifications`:

```mermaid
sequenceDiagram
    participant Webhook as TicketPurchaseService (Stripe webhook)
    participant SoldQ as queue: ticket-sold-notifications
    participant SoldConsumer as TicketSoldNotificationConsumer
    participant PurchasedQ as queue: ticket-purchased-notifications
    participant PurchasedConsumer as TicketPurchasedNotificationConsumer
    participant SMTP as SmtpEmailSender

    Webhook->>SoldQ: publish SendEmailMessage {To: seller, no attachment}
    Webhook->>PurchasedQ: publish SendEmailMessage {To: buyer, AttachmentPath: ticket PDF}
    SoldQ->>SoldConsumer: deliver
    SoldConsumer->>SMTP: SendAsync(...)
    PurchasedQ->>PurchasedConsumer: deliver
    PurchasedConsumer->>SMTP: SendAsync(..., attachmentPath)
```

`TicketAvailableMessage` and `SendEmailMessage` are plain DTOs serialized as JSON
(`System.Text.Json`):

```csharp
// Messaging/TicketAvailableMessage.cs
public class TicketAvailableMessage
{
    public required Guid TicketId { get; set; }
    public required DateOnly Date { get; set; }
}

// Messaging/SendEmailMessage.cs
public class SendEmailMessage
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
}
```

`AttachmentPath`/`AttachmentFileName` are optional — when set, `SmtpEmailSender` reads the file
from R2 at send time (via `IFileStorage.OpenReadAsync`, see [R2_STORAGE.md](R2_STORAGE.md)) and
attaches it (as `application/pdf`) rather than the message carrying the file bytes itself; if
the file is missing it logs a warning and sends the email without the attachment instead of
failing the whole message.

## Why go through a queue at all, in a single process?

`TicketAvailableConsumer` and `EmailNotificationConsumer` both run as `BackgroundService`s in the
same process that publishes to them. The queue hop isn't about crossing a service boundary —
it's what keeps subscriber-matching DB queries or a slow SMTP server from running synchronously
on the request thread that triggered them. Publish is fire-and-forget from the caller's point of
view; the actual work happens later, off the request path, in a background consumer loop.

## Connection handling

`RabbitMqConnectionProvider` (`Messaging/RabbitMqConnectionProvider.cs`):

- Registered as a **singleton**, holding one lazily-created `IConnection` for the app's lifetime.
- `GetConnectionAsync` double-checks `IsOpen` under a `SemaphoreSlim` lock so concurrent callers
  don't race to open multiple connections, and a dropped connection gets recreated on next use.
- Publishers and consumers each call `connection.CreateChannelAsync()` to get their own channel
  off the shared connection (channels are not reused/pooled).

Publishers (`RabbitMqTicketAvailablePublisher`, `RabbitMqEmailMessagePublisher`,
`RabbitMqTicketSoldNotificationPublisher`, `RabbitMqTicketPurchasedNotificationPublisher`) are
registered `Scoped` and open a fresh channel
per publish call, declaring the target queue (`durable: true, exclusive: false,
autoDelete: false`) before publishing — that queue-declare is what actually creates the queue on
first run; nothing provisions queues out-of-band. The two notification publishers are otherwise
identical to `RabbitMqEmailMessagePublisher` — same `SendEmailMessage` DTO, just a different
target queue — they exist as separate classes/interfaces purely so `TicketPurchaseService` can
inject "publish to the seller queue" and "publish to the buyer queue" as two distinct
dependencies.

Consumers (`TicketAvailableConsumer`, `EmailNotificationConsumer`,
`TicketSoldNotificationConsumer`, `TicketPurchasedNotificationConsumer`) are `BackgroundService`s
that open one long-lived channel in `ExecuteAsync`, declare the same queue, and register an
`AsyncEventingBasicConsumer`. They ack manually after successful processing and `nack` with
`requeue: true` on any exception — so a failing handler (e.g. SMTP down) leaves the message on
the queue to retry rather than dropping it, at the cost of possible redelivery/duplicate sends
once the dependency recovers.

`GetConnectionAsync` also retries the initial `CreateConnectionAsync` call itself: up to 5
attempts with exponential backoff (1s, 2s, 4s, 8s between attempts, ~15s total), logging a
warning on each retry. If every attempt fails, the exception propagates — for a
`BackgroundService`, an unhandled exception in `ExecuteAsync` stops the whole app, so a
persistently unreachable/misconfigured broker still fails loudly rather than hanging forever.
This exists because there's no `depends_on` ordering between `api` and the broker (see
[Running it locally](#running-it-locally) and [Production](#production)) — the retry absorbs a
local broker still booting, or a brief network blip to an external one.

## Configuration

`RabbitMqOptions` and `SmtpOptions` bind from `"RabbitMq"` and `"Smtp"` sections in
`appsettings.json`:

```json
"RabbitMq": {
  "Url": "amqp://guest:guest@rabbitmq:5672/",
  "TicketAvailableQueueName": "ticket-available",
  "EmailNotificationQueueName": "email-notifications",
  "TicketSoldNotificationQueueName": "ticket-sold-notifications",
  "TicketPurchasedNotificationQueueName": "ticket-purchased-notifications"
},
"Smtp": {
  "Host": "mailhog",
  "Port": 1025,
  "Username": "",
  "Password": "",
  "EnableSsl": false,
  "FromAddress": "no-reply@manassa-ticket.local"
}
```

`RabbitMqConnectionProvider` builds its `ConnectionFactory` directly from `Url` via
`factory.Uri = new Uri(options.Value.Url)`, which parses host/port/user/pass/vhost from the URL
and enables TLS automatically for an `amqps://` scheme (vs. plain `amqp://` — no separate TLS
flag needed).

The dev default (`amqp://guest:guest@rabbitmq:5672/`) is overridden via `RabbitMq__Url` in `.env`
for production (`Smtp__*` similarly). `rabbitmq` in the dev default is the Docker Compose service
name — this only resolves inside the compose network, not from the host machine. In production
there is no local `rabbitmq` container; `RabbitMq__Url` points at an external broker instead —
see [Production](#production).

## Running it locally

`docker compose -f compose.yaml -f compose.dev.yaml --profile dev up --build` starts a local
`rabbitmq:3-management` container alongside `api`, with a healthcheck
(`rabbitmq-diagnostics ping`). Like `mailhog` and `stripe-cli`, `rabbitmq` is gated behind the
`dev` Compose profile (`COMPOSE_PROFILES=dev` in `.env`) — it is not part of the production
stack (`compose.prod.yaml`), and `api` no longer has a `depends_on` on it (see the connection
retry note above for why that's safe).

- AMQP port: `5672`
- Management UI: [http://localhost:15672](http://localhost:15672) (same `guest`/`guest`
  credentials, or whatever you set in `.env`) — useful for inspecting queue depth, unacked
  messages, or manually publishing a test message.

## Production

Production does not run a local broker. `.env` sets `RabbitMq__Url` to the full connection URL
of an external managed RabbitMQ broker (e.g. CloudAMQP, Amazon MQ, or similar hosted provider),
using `amqps://` (not `amqp://`) — managed brokers are reached over the public internet on a TLS
port (typically `5671`) with a provider-assigned vhost, not `/`. No code or compose changes are
needed to switch between dev and production — only `.env`, consistent with the rest of this
project's deployment story (see the root `README.md`'s Deployment section).

## File map

| File | Role |
|---|---|
| `Messaging/RabbitMqConnectionProvider.cs` | shared singleton connection |
| `Messaging/RabbitMqTicketAvailablePublisher.cs` | publishes `ticket-available` |
| `Messaging/TicketAvailableConsumer.cs` | consumes `ticket-available` |
| `Messaging/RabbitMqEmailMessagePublisher.cs` | publishes `email-notifications` |
| `Messaging/EmailNotificationConsumer.cs` | consumes `email-notifications`, calls `IEmailSender` |
| `Messaging/RabbitMqTicketSoldNotificationPublisher.cs` | publishes `ticket-sold-notifications` |
| `Messaging/TicketSoldNotificationConsumer.cs` | consumes `ticket-sold-notifications`, calls `IEmailSender` |
| `Messaging/RabbitMqTicketPurchasedNotificationPublisher.cs` | publishes `ticket-purchased-notifications` |
| `Messaging/TicketPurchasedNotificationConsumer.cs` | consumes `ticket-purchased-notifications`, calls `IEmailSender` |
| `Services/Email/SmtpEmailSender.cs` | sends the email via SMTP |
| `Services/Tickets/TicketPoster.cs` | calls `ITicketProcessor` directly (no queue) to post a ticket |
| `Services/Tickets/TicketProcessor.cs`, `TicketDeleter.cs` | trigger `ticket-available` on listing changes |
| `Services/Notifications/TicketAvailableNotifier.cs` | matches subscribers, triggers `email-notifications` |
| `Services/Contact/ContactUsService.cs` | triggers `email-notifications` directly |
| `Services/Payments/TicketPurchaseService.cs` | triggers `ticket-sold-notifications`/`ticket-purchased-notifications` from the Stripe webhook |
| `Configuration/RabbitMqOptions.cs`, `SmtpOptions.cs` | bound config |
