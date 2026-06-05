using System.Globalization;
using System.Text;
using MATGER.Api.Data;
using MATGER.Api.DTOs.CommerceOperations;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/commerce-operations")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class CommerceOperationsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("abandoned-carts")]
    public async Task<ActionResult<PaginatedResponse<AbandonedCartResponse>>> GetAbandonedCarts(
        [FromQuery] int olderThanDays = 2,
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (olderThanDays < 1)
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, "Older-than days must be at least 1."));
        }

        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var threshold = DateTime.UtcNow.AddDays(-olderThanDays);

        var query = dbContext.Carts
            .AsNoTracking()
            .Include(cart => cart.User)
            .Include(cart => cart.Items)
            .Where(cart =>
                cart.Status == CartStatus.Active &&
                cart.Items.Count > 0 &&
                cart.CreatedAt <= threshold);

        var totalCount = await query.CountAsync(cancellationToken);

        var carts = await query
            .OrderBy(cart => cart.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cart => new AbandonedCartResponse
            {
                CartId = cart.Id,
                UserId = cart.UserId,
                CustomerEmail = cart.User.Email ?? string.Empty,
                CustomerFullName = cart.User.FullName,
                ItemsCount = cart.Items.Count,
                CartValue = cart.Items.Sum(item => item.UnitPriceSnapshot * item.Quantity),
                CreatedAt = cart.CreatedAt,
                ExpiresAt = cart.ExpiresAt,
                LastActivityAt = cart.Items.Max(item => item.UpdatedAt ?? item.CreatedAt),
                AgeInDays = EF.Functions.DateDiffDay(cart.CreatedAt, DateTime.UtcNow)
            })
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<AbandonedCartResponse>.Create(
            carts,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("customer-segments")]
    public async Task<ActionResult<PaginatedResponse<CustomerSegmentResponse>>> GetCustomerSegments(
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.Users
            .AsNoTracking()
            .Select(user => new CustomerSegmentResponse
            {
                UserId = user.Id,
                CustomerEmail = user.Email ?? string.Empty,
                CustomerFullName = user.FullName,
                OrdersCount = dbContext.Orders.Count(order => order.UserId == user.Id),
                DeliveredOrdersCount = dbContext.Orders.Count(order =>
                    order.UserId == user.Id &&
                    order.Status == OrderStatus.Delivered),
                RefundsCount = dbContext.Refunds.Count(refund => refund.Order.UserId == user.Id),
                TotalSpent = dbContext.Orders
                    .Where(order =>
                        order.UserId == user.Id &&
                        (order.Status == OrderStatus.Paid ||
                         order.Status == OrderStatus.Processing ||
                         order.Status == OrderStatus.Shipped ||
                         order.Status == OrderStatus.Delivered))
                    .Select(order => (decimal?)order.Total)
                    .Sum() ?? 0m,
                LastOrderAt = dbContext.Orders
                    .Where(order => order.UserId == user.Id)
                    .Select(order => (DateTime?)order.CreatedAt)
                    .Max(),
                Segment = (dbContext.Orders
                    .Where(order =>
                        order.UserId == user.Id &&
                        (order.Status == OrderStatus.Paid ||
                         order.Status == OrderStatus.Processing ||
                         order.Status == OrderStatus.Shipped ||
                         order.Status == OrderStatus.Delivered))
                    .Select(order => (decimal?)order.Total)
                    .Sum() ?? 0m) >= 1000m
                    ? "HighValue"
                    : dbContext.Orders.Count(order => order.UserId == user.Id) >= 3
                        ? "RepeatCustomer"
                        : dbContext.Orders.Any(order => order.UserId == user.Id)
                            ? "OneTimeCustomer"
                            : "NoOrders"
            });

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderByDescending(customer => customer.TotalSpent)
            .ThenByDescending(customer => customer.OrdersCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<CustomerSegmentResponse>.Create(
            customers,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("stock-reconciliation")]
    public async Task<ActionResult<IReadOnlyList<StockReconciliationIssueResponse>>> GetStockReconciliation(
        CancellationToken cancellationToken)
    {
        var productIssues = await dbContext.InventoryItems
            .AsNoTracking()
            .Include(item => item.Product)
            .Select(item => new StockReconciliationIssueResponse
            {
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                ProductVariantId = null,
                VariantName = null,
                SKU = item.Product.SKU,
                ActualReservedQuantity = item.QuantityReserved,
                ExpectedReservedQuantity = dbContext.InventoryReservations
                    .Where(reservation =>
                        reservation.ProductId == item.ProductId &&
                        reservation.ProductVariantId == null &&
                        reservation.Status == InventoryReservationStatus.Pending)
                    .Sum(reservation => (int?)reservation.Quantity) ?? 0,
                Scope = "Product"
            })
            .Where(issue => issue.ActualReservedQuantity != issue.ExpectedReservedQuantity)
            .ToListAsync(cancellationToken);

        var variantIssues = await dbContext.ProductVariants
            .AsNoTracking()
            .Include(variant => variant.Product)
            .Select(variant => new StockReconciliationIssueResponse
            {
                ProductId = variant.ProductId,
                ProductName = variant.Product.Name,
                ProductVariantId = variant.Id,
                VariantName = variant.Name,
                SKU = variant.SKU,
                ActualReservedQuantity = variant.QuantityReserved,
                ExpectedReservedQuantity = dbContext.InventoryReservations
                    .Where(reservation =>
                        reservation.ProductVariantId == variant.Id &&
                        reservation.Status == InventoryReservationStatus.Pending)
                    .Sum(reservation => (int?)reservation.Quantity) ?? 0,
                Scope = "Variant"
            })
            .Where(issue => issue.ActualReservedQuantity != issue.ExpectedReservedQuantity)
            .ToListAsync(cancellationToken);

        return Ok(productIssues.Concat(variantIssues).ToList());
    }

    [HttpPost("stock-reconciliation/fix")]
    public async Task<ActionResult<IReadOnlyList<StockReconciliationIssueResponse>>> ReconcileStock(
        CancellationToken cancellationToken)
    {
        var issuesBeforeFix = new List<StockReconciliationIssueResponse>();

        var inventoryItems = await dbContext.InventoryItems
            .Include(item => item.Product)
            .ToListAsync(cancellationToken);

        foreach (var item in inventoryItems)
        {
            var expectedReserved = await dbContext.InventoryReservations
                .Where(reservation =>
                    reservation.ProductId == item.ProductId &&
                    reservation.ProductVariantId == null &&
                    reservation.Status == InventoryReservationStatus.Pending)
                .SumAsync(reservation => (int?)reservation.Quantity, cancellationToken) ?? 0;

            if (item.QuantityReserved == expectedReserved)
            {
                continue;
            }

            issuesBeforeFix.Add(new StockReconciliationIssueResponse
            {
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                SKU = item.Product.SKU,
                ActualReservedQuantity = item.QuantityReserved,
                ExpectedReservedQuantity = expectedReserved,
                Scope = "Product"
            });

            item.QuantityReserved = expectedReserved;
            item.UpdatedAt = DateTime.UtcNow;
        }

        var variants = await dbContext.ProductVariants
            .Include(variant => variant.Product)
            .ToListAsync(cancellationToken);

        foreach (var variant in variants)
        {
            var expectedReserved = await dbContext.InventoryReservations
                .Where(reservation =>
                    reservation.ProductVariantId == variant.Id &&
                    reservation.Status == InventoryReservationStatus.Pending)
                .SumAsync(reservation => (int?)reservation.Quantity, cancellationToken) ?? 0;

            if (variant.QuantityReserved == expectedReserved)
            {
                continue;
            }

            issuesBeforeFix.Add(new StockReconciliationIssueResponse
            {
                ProductId = variant.ProductId,
                ProductName = variant.Product.Name,
                ProductVariantId = variant.Id,
                VariantName = variant.Name,
                SKU = variant.SKU,
                ActualReservedQuantity = variant.QuantityReserved,
                ExpectedReservedQuantity = expectedReserved,
                Scope = "Variant"
            });

            variant.QuantityReserved = expectedReserved;
            variant.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(issuesBeforeFix);
    }


    [HttpPost("imports/products.csv")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<object>> ImportProducts(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, "CSV file is required."));
        }

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var header = await reader.ReadLineAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(header))
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, "CSV file has no header row."));
        }

        if (!TryParseCsvLine(header, out var headerColumns, out var headerError) || headerColumns.Count < 5)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                headerError ?? "CSV header is invalid."));
        }

        var createdProducts = 0;
        var skippedRows = 0;
        var rowNumber = 1;
        var errors = new List<string>();
        var importedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            rowNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryParseCsvLine(line, out var columns, out var csvParseError))
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: {csvParseError}");
                continue;
            }

            if (columns.Count < 8)
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: expected at least 8 columns.");
                continue;
            }

            var name = columns[0].Trim();
            var description = columns[1].Trim();
            var normalizedSku = columns[2].Trim().ToUpperInvariant();
            var categorySlug = columns[4].Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(description) ||
                string.IsNullOrWhiteSpace(normalizedSku) ||
                string.IsNullOrWhiteSpace(categorySlug))
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: name, description, SKU, and category slug are required.");
                continue;
            }

            if (!decimal.TryParse(columns[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0)
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: price must be greater than zero.");
                continue;
            }

            if (!importedSkus.Add(normalizedSku))
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: duplicate SKU '{normalizedSku}' exists in the uploaded CSV.");
                continue;
            }

            var skuExists = await dbContext.Products.AnyAsync(product => product.SKU == normalizedSku, cancellationToken) ||
                            await dbContext.ProductVariants.AnyAsync(variant => variant.SKU == normalizedSku, cancellationToken);

            if (skuExists)
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: SKU '{normalizedSku}' already exists.");
                continue;
            }

            var category = await dbContext.Categories
                .FirstOrDefaultAsync(category => category.Slug == categorySlug, cancellationToken);

            if (category is null)
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: category slug '{categorySlug}' was not found.");
                continue;
            }

            if (!TryParseOptionalInt(columns, 5, 0, out var quantityAvailable) || quantityAvailable < 0)
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: quantity available must be a non-negative whole number.");
                continue;
            }

            if (!TryParseOptionalInt(columns, 6, 5, out var lowStockThreshold) || lowStockThreshold < 0)
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: low stock threshold must be a non-negative whole number.");
                continue;
            }

            if (!TryParseOptionalBool(columns, 7, true, out var isActive))
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: is active must be true or false.");
                continue;
            }

            if (!TryParseOptionalBool(columns, 8, false, out var isFeatured))
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: is featured must be true or false.");
                continue;
            }

            if (!TryParseOptionalBool(columns, 9, true, out var isReturnable))
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: is returnable must be true or false.");
                continue;
            }

            if (!TryParseOptionalInt(columns, 10, 14, out var returnWindowDays) || returnWindowDays < 1)
            {
                skippedRows++;
                errors.Add($"Row {rowNumber}: return window days must be at least 1.");
                continue;
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                SKU = normalizedSku,
                Price = price,
                CategoryId = category.Id,
                IsActive = isActive,
                IsFeatured = isFeatured && isActive && category.IsActive,
                IsReturnable = isReturnable,
                ReturnWindowDays = Math.Max(1, returnWindowDays),
                CreatedAt = DateTime.UtcNow,
                InventoryItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    QuantityAvailable = quantityAvailable,
                    QuantityReserved = 0,
                    LowStockThreshold = lowStockThreshold,
                    CreatedAt = DateTime.UtcNow
                }
            };

            product.InventoryItem!.ProductId = product.Id;
            product.InventoryItem!.Product = product;

            await dbContext.Products.AddAsync(product, cancellationToken);
            createdProducts++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            CreatedProducts = createdProducts,
            SkippedRows = skippedRows,
            Errors = errors.Take(50).ToList()
        });
    }

    [HttpGet("exports/products.csv")]
    public async Task<IActionResult> ExportProducts(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Include(product => product.InventoryItem)
            .OrderBy(product => product.Name)
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.SKU,
                product.Price,
                Category = product.Category.Name,
                product.IsActive,
                product.IsFeatured,
                product.IsReturnable,
                product.ReturnWindowDays,
                QuantityAvailable = product.InventoryItem == null ? 0 : product.InventoryItem.QuantityAvailable,
                QuantityReserved = product.InventoryItem == null ? 0 : product.InventoryItem.QuantityReserved
            })
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Id,Name,SKU,Price,Category,IsActive,IsFeatured,IsReturnable,ReturnWindowDays,QuantityAvailable,QuantityReserved");

        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Escape(row.Id.ToString()),
                Escape(row.Name),
                Escape(row.SKU),
                Escape(row.Price.ToString("0.00")),
                Escape(row.Category),
                Escape(row.IsActive.ToString()),
                Escape(row.IsFeatured.ToString()),
                Escape(row.IsReturnable.ToString()),
                Escape(row.ReturnWindowDays.ToString()),
                Escape(row.QuantityAvailable.ToString()),
                Escape(row.QuantityReserved.ToString())
            }));
        }

        return File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv",
            "matger-products.csv");
    }

    [HttpGet("exports/inventory.csv")]
    public async Task<IActionResult> ExportInventory(CancellationToken cancellationToken)
    {
        var productRows = await dbContext.InventoryItems
            .AsNoTracking()
            .Include(item => item.Product)
            .OrderBy(item => item.Product.Name)
            .Select(item => new
            {
                Scope = "Product",
                Product = item.Product.Name,
                Variant = string.Empty,
                SKU = item.Product.SKU,
                item.QuantityAvailable,
                item.QuantityReserved,
                item.LowStockThreshold
            })
            .ToListAsync(cancellationToken);

        var variantRows = await dbContext.ProductVariants
            .AsNoTracking()
            .Include(variant => variant.Product)
            .OrderBy(variant => variant.Product.Name)
            .ThenBy(variant => variant.Name)
            .Select(variant => new
            {
                Scope = "Variant",
                Product = variant.Product.Name,
                Variant = variant.Name,
                SKU = variant.SKU,
                variant.QuantityAvailable,
                variant.QuantityReserved,
                variant.LowStockThreshold
            })
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Scope,Product,Variant,SKU,QuantityAvailable,QuantityReserved,LowStockThreshold");

        foreach (var row in productRows.Concat(variantRows))
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Escape(row.Scope),
                Escape(row.Product),
                Escape(row.Variant),
                Escape(row.SKU),
                Escape(row.QuantityAvailable.ToString()),
                Escape(row.QuantityReserved.ToString()),
                Escape(row.LowStockThreshold.ToString())
            }));
        }

        return File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv",
            "matger-inventory.csv");
    }


    private static bool TryParseCsvLine(
        string line,
        out List<string> values,
        out string? error)
    {
        values = [];
        error = null;
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (inQuotes)
        {
            error = "CSV row has an unterminated quoted value.";
            return false;
        }

        values.Add(current.ToString());

        return true;
    }

    private static bool TryParseOptionalInt(
        IReadOnlyList<string> values,
        int index,
        int fallback,
        out int parsed)
    {
        if (values.Count <= index || string.IsNullOrWhiteSpace(values[index]))
        {
            parsed = fallback;
            return true;
        }

        return int.TryParse(
            values[index].Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out parsed);
    }

    private static bool TryParseOptionalBool(
        IReadOnlyList<string> values,
        int index,
        bool fallback,
        out bool parsed)
    {
        if (values.Count <= index || string.IsNullOrWhiteSpace(values[index]))
        {
            parsed = fallback;
            return true;
        }

        return bool.TryParse(values[index].Trim(), out parsed);
    }

    private ApiErrorResponse Error(int statusCode, string message)
    {
        return new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private static string Escape(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);

        return $"\"{escaped}\"";
    }
}
