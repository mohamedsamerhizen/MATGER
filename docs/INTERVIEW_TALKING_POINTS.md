# Interview Talking Points

## What MATGER Demonstrates

MATGER demonstrates that a readable two-project ASP.NET Core API can still model real commercial backend workflows.

## Strong Points

- JWT + refresh token authentication.
- Role-based authorization and ownership checks.
- EF Core data model for commerce workflows.
- Checkout with inventory reservations.
- Mock payment flow with idempotency protection.
- Order state transitions.
- Returns/refunds with double-action protection.
- Cost snapshots and profit reporting.
- Sale windows and price history.
- Reorder planning.
- Stock adjustment approval workflow.
- Customer 360 and internal notes.
- Risk signal recording.
- Wallet/store credit and loyalty points.
- Large demo seed dataset.
- Integration tests covering security and business rules.

## Why No Clean Architecture?

The project intentionally avoids adding extra layers because the target is a focused backend engine, not an architecture showcase. This keeps the code easier to inspect in interviews while still proving business logic, authorization, and data integrity.

## Why Mock Payments?

The payment system is intentionally mocked because the goal is to show payment workflow design without handling real money or external gateway credentials.

## What I Would Improve Next

- Real payment gateway adapter behind a provider abstraction.
- Email/SMS notifications behind interfaces.
- Audit export and retention policies.
- Background job scheduler for advanced maintenance tasks.
- Admin UI or API client demo.
- Production secret management.
