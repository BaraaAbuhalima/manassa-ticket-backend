# manassa-tickets-exchange-backend
Backend service powering Manassa Ticket Exchange — secure ticket marketplace with payments, orders, and user management.

## Git hooks

Run `scripts\install-git-hooks.ps1` once to enable the pre-commit formatter hook.

## Local development

Copy `.env.example` to `.env` and fill in the values, then run `scripts\dev.ps1` instead of
`docker compose up` to start the stack — a `stripe-cli` container forwards Stripe webhooks to
the api service automatically. No local Stripe CLI install needed.

## Deployment

All config that differs between environments (secrets, and dev vs production behavior) lives
in `.env`, not in source. `appsettings.json` only holds non-secret defaults.

1. Copy `.env.example` to `.env`.
2. Fill in real values for the `Stripe__*`, `Jwt__SigningKey`, `RabbitMq__*`, `Smtp__*`, and
   `Storage__R2__*` entries — see the comments in `.env.example` for where each one comes from,
   and [R2_STORAGE.md](R2_STORAGE.md) for setting up the Cloudflare R2 buckets and API tokens.
3. Set `ASPNETCORE_ENVIRONMENT` to `Development` or `Production`.
4. Set `COMPOSE_PROFILES=dev` to also start `rabbitmq` (local broker), `mailhog` (fake SMTP),
   and `stripe-cli` (webhook forwarder) for local testing. Leave it blank/unset in production —
   all three are dev-only; production connects to an external managed RabbitMQ broker and a
   real SMTP provider instead (see `RabbitMq__*`/`Smtp__*` in `.env.example` and
   [RABBITMQ.md](RABBITMQ.md#production)), and webhooks should be registered against your real
   public HTTPS domain in the Stripe Dashboard.
5. Run `docker compose up --build`.

No file changes are needed between dev and production — only `.env`.

**Known gap:** the app currently runs on an in-memory SQLite database
(`manassa-ticket-backend/Program.cs`), so all data is lost on every restart. This needs to move
to a persistent database provider before a real production deployment — ask if you'd like help
with that.

## GitHub Actions

The `Tests` workflow runs on pull requests and pushes to `main`. To block merges to `main`, make it a required status check in branch protection settings.