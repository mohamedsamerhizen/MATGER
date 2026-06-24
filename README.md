# MATGER — Advanced E-commerce Backend Engine

MATGER is a production-style ASP.NET Core Web API for an e-commerce backend. It keeps a deliberately simple two-project structure while covering real commerce workflows: authentication, catalog, cart, checkout, mock payments, inventory, fulfillment, reporting, risk signals, wallet/store credit, loyalty points, demo seed data, Docker, CI, and integration tests.

The goal is not to show a basic CRUD project. The goal is to demonstrate backend judgement: authorization, state transitions, transaction safety, pricing snapshots, stock consistency, reporting, idempotency, and test coverage.

## Project Structure

```text
MATGER/
  MATGER.Api/      ASP.NET Core API, EF Core DbContext, Identity, controllers, services, DTOs
  MATGER.Tests/    xUnit integration tests using WebApplicationFactory and EF Core InMemory
  docs/            demo flows, workflows, security notes, GitHub checklist
```

MATGER intentionally avoids Clean Architecture, MediatR, CQRS, frontend/mobile code, external queues, real payment gateways, SMS, and email providers. The architecture is intentionally readable for reviewers.

## Tech Stack

- .NET 10 / ASP.NET Core Web API
- EF Core with SQL Server for development/runtime
- EF Core InMemory provider for integration tests
- ASP.NET Core Identity with role-based authorization
- JWT access tokens and refresh tokens
- Swagger/OpenAPI
- Serilog console/file logging
- Docker Compose for SQL Server
- xUnit integration tests
- GitHub Actions CI

## Main Roles

- `Admin`: full back-office access.
- `Customer`: shopping, cart, checkout, orders, wallet, loyalty, wishlist, reviews.
- `OrderManager`: fulfillment and order operations.
- `InventoryManager`: inventory and stock-adjustment workflows.

## Demo Accounts

Development/demo seed creates these accounts:

| Role | Email | Password |
|---|---|---|
| Admin | `admin@matger.local` | `Admin12345` |
| Order Manager | `order.manager@matger.local` | `Demo12345` |
| Inventory Manager | `inventory.manager@matger.local` | `Demo12345` |
| First Customer | `customer01@matger.local` | `Demo12345` |

Demo seed runs only in Development when enabled.

## Core Features

### Authentication and Security

- Register, login, refresh token, logout, and current user endpoints.
- Role policies for Admin, Customer, OrderManager, and InventoryManager.
- Inactive-user checks.
- Rate limiting for authentication endpoints.
- Swagger JWT bearer support.
- Global exception middleware with consistent API errors.
- Request tracing middleware.

### Catalog

- Categories, brands, products, variants, images, specifications, featured/new-arrival listing.
- Search, category filtering, brand filtering, price filtering using effective price, active-sale filtering, sorting, and pagination.
- Public responses hide internal cost price.
- Admin endpoints for product images, product specifications, sale windows, and price history.

### Pricing and Profit

- `CostPrice` and `CostPriceSnapshot` support.
- Sale windows with active/upcoming/expired logic.
- Effective price calculation.
- Price history records.
- Profit reports by product/category and low-margin visibility.

### Cart and Checkout

- Authenticated customer cart.
- Variant-aware items.
- Coupon application.
- Checkout creates orders, payments, payment attempts, and inventory reservations.
- Checkout stores price and cost snapshots.
- Mock payment confirmation/failure.
- Idempotency support for sensitive payment confirmation.

### Orders, Fulfillment, Returns, Refunds

- Customer order list/details with ownership checks.
- Admin order summary, timeline, status history, and internal notes.
- OrderManager fulfillment transitions: processing, shipped, delivered.
- Picking list and order-level picking list.
- CSV order export.
- Customer return requests for eligible delivered orders.
- Admin return approval/rejection/completion.
- Admin refund protection against duplicate/unpaid refunds.

### Inventory

- Inventory listing and movements.
- Inventory reservation cleanup background service.
- Reorder planning with supplier data, reorder point, suggested reorder quantity, lead time, bin location, and severity.
- Stock adjustment approval workflow with Pending/Approved/Rejected/Cancelled states.
- Approval updates inventory and creates movement records.

### Customer Intelligence

- Admin Customer 360 profile.
- Customer segment calculation.
- Internal customer notes visible only to Admin.
- Risk signals for suspicious order/customer behavior.

### Wallet and Loyalty

- Customer wallet/store credit balance and transactions.
- Admin wallet credit/debit with transaction recording and negative-balance protection.
- Customer loyalty account and transactions.
- Admin loyalty points adjustment and summary.
- Loyalty points awarded on delivered orders.

### Operations Dashboard

Admin dashboard includes:

- Operations summary.
- Sales overview.
- Inventory overview.
- Sales report.
- Profit report.
- Revenue chart.
- Top products.
- Order status breakdown.
- Coupon performance.
- Customer insights.

### Demo Seed Data

The demo seed creates a realistic commercial dataset:

- 24+ customers.
- 10+ categories.
- 120+ products.
- Product variants, brands, images, specifications.
- Cost prices, sale windows, price history.
- Inventory, reorder data, low/critical/healthy stock.
- 240+ orders with mixed states.
- Payments, attempts, shipping, coupons, carts, wishlist, reviews.
- Returns/refunds, inventory movements, stock adjustment requests.
- Customer notes, risk signals, wallets, loyalty records.

The seeder is designed to be safe on repeated runs.

## Run Locally

```powershell
cd C:\Users\lenovo\Desktop\MATGER

copy .env.example .env

docker compose up -d

dotnet restore .\MATGER.sln

dotnet ef database update `
  --project .\MATGER.Api\MATGER.Api.csproj `
  --startup-project .\MATGER.Api\MATGER.Api.csproj

dotnet run --project .\MATGER.Api\MATGER.Api.csproj
```

Swagger:

```text
http://localhost:5179/swagger
```

## Run Tests

```powershell
cd C:\Users\lenovo\Desktop\MATGER

dotnet restore .\MATGER.sln
dotnet build .\MATGER.sln
dotnet test .\MATGER.sln
```

Current expected test count after final expansion stage: `45/45` before any future additions.

## Important Notes

- The project uses a mock payment flow, not a real payment gateway.
- Demo credentials are for local development only.
- Do not commit `.env`, `bin/`, `obj/`, `logs/`, `TestResults/`, or generated ZIP files.
- Replace development JWT secrets and SQL credentials before any real deployment.
