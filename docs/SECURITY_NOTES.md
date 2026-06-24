# Security Notes

MATGER is a portfolio-grade backend project, not a production-hosted service. It still includes important security practices that reviewers can inspect.

## Implemented

- JWT authentication.
- Refresh tokens.
- ASP.NET Core Identity.
- Role-based authorization policies.
- Ownership checks for customer resources.
- Admin-only access for sensitive reports and operations.
- Rate limiting on authentication routes.
- Global exception handling.
- Swagger bearer token configuration.
- Public catalog responses hide cost price.
- Customer cannot access internal notes, risk signals, admin reports, another customer's wallet, another customer's loyalty account, or another customer's orders.

## Local Development Secrets

The repository may contain development placeholders in `appsettings.json` and `.env.example` so the project can run locally. For real deployment:

- Move connection strings to environment variables or a secret manager.
- Rotate the SQL password.
- Replace the JWT signing key with a long random secret.
- Disable demo seed in production.
- Do not commit `.env`.

## Git Hygiene

Ignored files include:

- `bin/`
- `obj/`
- `logs/`
- `.env`
- `TestResults/`
- `coverage/`
- `*.zip`

## Limitations by Design

- No real payment gateway.
- No SMS/email provider.
- No external queue.
- No frontend.
- No multi-tenant production isolation.
