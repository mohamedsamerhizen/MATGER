# Screenshot Capture Checklist

Capture real screenshots only. Do not use mock or generated screenshots for the final GitHub README.

Save these files in this folder:

| Filename | What to capture |
| --- | --- |
| `swagger-home.png` | Swagger landing page showing the MATGER API title and endpoint groups. |
| `swagger-auth.png` | Swagger `Auth` endpoints expanded enough to show register, login, refresh, logout, and current-user routes. |
| `swagger-products.png` | Swagger product/catalog endpoints, including products, featured products, and product variants. |
| `swagger-orders.png` | Swagger order endpoints, including customer order routes and fulfillment/admin order actions. |
| `swagger-admin-dashboard.png` | Swagger admin dashboard/reporting endpoints. |
| `swagger-commerce-operations.png` | Swagger commerce operations endpoints, including stock reconciliation and CSV import/export. |
| `tests-passed.png` | Terminal output showing `dotnet test .\MATGER.sln` passed with 12/12 tests. |
| `docker-running.png` | Docker Desktop or terminal output showing the SQL Server container healthy/running. |
| `database-update-done.png` | Terminal output showing `dotnet ef database update --project .\MATGER.Api --startup-project .\MATGER.Api` completed successfully. |

Recommended capture order:

1. Start SQL Server with `docker compose up -d matger-sqlserver`.
2. Apply migrations with `dotnet ef database update --project .\MATGER.Api --startup-project .\MATGER.Api`.
3. Run the API with `dotnet run --project .\MATGER.Api`.
4. Open Swagger in Development and capture the Swagger screenshots.
5. Run `dotnet test .\MATGER.sln` and capture the passing test output.
