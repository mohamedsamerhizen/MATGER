# API Demo Flow

This is a suggested reviewer/demo path through the API.

## 1. Start Infrastructure

```powershell
docker compose up -d
dotnet ef database update --project .\MATGER.Api\MATGER.Api.csproj --startup-project .\MATGER.Api\MATGER.Api.csproj
dotnet run --project .\MATGER.Api\MATGER.Api.csproj
```

Open:

```text
http://localhost:5179/swagger
```

## 2. Login

Use:

```http
POST /api/auth/login
```

Admin:

```json
{
  "email": "admin@matger.local",
  "password": "Admin12345"
}
```

Customer:

```json
{
  "email": "customer01@matger.local",
  "password": "Demo12345"
}
```

Copy the access token into Swagger authorization.

## 3. Public Catalog

- `GET /api/products`
- `GET /api/products?search=phone`
- `GET /api/products?activeSaleOnly=true&sortBy=price_asc`
- `GET /api/products/{id}`

Show that public responses contain presentation data but do not expose cost price.

## 4. Customer Shopping

- `GET /api/cart`
- `POST /api/cart/items`
- `POST /api/cart/coupon`
- `POST /api/checkout/start`
- `POST /api/checkout/confirm-payment`
- `GET /api/orders`

## 5. Admin Operations

- `GET /api/admin/dashboard/stats`
- `GET /api/admin/dashboard/operations-summary`
- `GET /api/admin/dashboard/sales-overview`
- `GET /api/admin/dashboard/inventory-overview`
- `GET /api/admin/dashboard/profit-report`
- `GET /api/admin/inventory/reorder-needed`
- `GET /api/admin/risk-signals/open`
- `GET /api/admin/demo-data/summary`

## 6. Fulfillment

- `GET /api/admin/fulfillment/picking-list`
- `GET /api/admin/fulfillment/orders/{orderId}/picking-list`
- `POST /api/orders/{id}/mark-processing`
- `POST /api/orders/{id}/mark-shipped`
- `POST /api/orders/{id}/mark-delivered`

## 7. Customer Intelligence

- `GET /api/admin/customers/{userId}/profile`
- `POST /api/admin/customers/{userId}/internal-notes`
- `GET /api/admin/customers/{userId}/internal-notes`

## 8. Wallet and Loyalty

- `GET /api/wallet`
- `GET /api/wallet/transactions`
- `POST /api/admin/customers/{userId}/wallet/credit`
- `POST /api/admin/customers/{userId}/wallet/debit`
- `GET /api/loyalty`
- `GET /api/loyalty/transactions`
- `POST /api/admin/customers/{userId}/loyalty/adjust`
- `GET /api/admin/loyalty/summary`
