# MATGER PASS 2 — Large Demo Seed Data

## What changed

PASS 2 adds a large professional demo data layer without changing the architecture and without adding migrations.

Added:

- Development-only automatic demo seeding.
- Manual admin endpoint to trigger demo seeding.
- Admin summary endpoint for current demo/data counts.
- 10 demo categories.
- 120 demo products by default.
- Product variants for many products.
- Inventory items and inventory movements.
- 24 demo customers by default.
- Customer addresses.
- Shipping methods.
- Coupons and coupon redemptions.
- Active carts and cart items.
- Wishlists.
- 240 demo orders by default.
- Order items.
- Payments and payment attempts.
- Inventory reservations.
- Return requests and refunds.
- Product reviews.
- Order status histories.
- Order internal notes.
- Audit logs.

## Configuration

The demo seed is controlled from `MATGER.Api/appsettings.json`:

```json
"DemoSeed": {
  "Enabled": true,
  "CustomerCount": 24,
  "ProductsPerCategory": 12,
  "OrderCount": 240,
  "RandomSeed": 20260623,
  "DemoPassword": "Demo12345"
}
```

It runs only in the Development environment during API startup.

## Demo credentials

| Role | Email | Password |
|---|---|---|
| Admin | admin@matger.local | Admin12345 |
| Order Manager | order.manager@matger.local | Demo12345 |
| Inventory Manager | inventory.manager@matger.local | Demo12345 |
| Customer | customer01@matger.local | Demo12345 |

Customer emails continue from `customer01@matger.local` to `customer24@matger.local` by default.

## Endpoints

```http
GET /api/admin/demo-data/summary
POST /api/admin/demo-data/seed
```

Both endpoints require an Admin token.

## Safety notes

- No migrations were added.
- The seeder is idempotent by checking for products whose SKU starts with `DEMO-`.
- It does not delete existing data.
- It does not run in Testing or Production unless manually configured and hosted as Development.
- To reseed from scratch, clear the database first, then run the API again.
