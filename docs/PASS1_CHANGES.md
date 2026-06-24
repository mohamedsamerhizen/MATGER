# MATGER PASS 1 — Modified Files Patch

This patch contains only the files changed in PASS 1. It does not include migrations and does not change the project architecture.

## Included changes

1. `MATGER.Api/Program.cs`
   - Adds Swagger JWT security requirement.
   - Adds Swagger tag grouping for cleaner API browsing.
   - Adds built-in rate limiting for sensitive auth endpoints:
     - `POST /api/auth/login`
     - `POST /api/auth/register`
     - `POST /api/auth/refresh`
   - Registers request tracing middleware.

2. `MATGER.Api/Middleware/RequestTracingMiddleware.cs`
   - Adds `X-Trace-Id` response header.
   - Adds `X-Request-Duration-Ms` response header.

3. `MATGER.Api/Services/AdminReportingService.cs`
   - Reduces dashboard stats query fan-out by grouping order status counts and revenue totals.
   - Keeps the existing response contract unchanged.

4. `.gitignore`
   - Adds stronger ignore rules for local archives and nested logs.

5. `docs/matger-phase1-demo.http`
   - Adds a local HTTP request collection for manual API testing.

## Not included

- No migrations.
- No entities added.
- No frontend.
- No payment gateway.
- No SMS/email.
- No architectural rewrite.
