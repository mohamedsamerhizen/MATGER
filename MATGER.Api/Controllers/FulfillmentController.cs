using System.Globalization;
using System.Text;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Fulfillment;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.OrderManagerOnly)]
public sealed class FulfillmentController(ApplicationDbContext dbContext) : ControllerBase
{
    private static readonly OrderStatus[] PickableStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Processing
    ];

    [HttpGet("fulfillment/picking-list")]
    public async Task<ActionResult<IReadOnlyList<PickingListItemResponse>>> GetPickingList(
        CancellationToken cancellationToken)
    {
        var items = await dbContext.OrderItems
            .AsNoTracking()
            .Include(item => item.Order)
            .Include(item => item.Product)
            .ThenInclude(product => product.InventoryItem)
            .Include(item => item.ProductVariant)
            .Where(item => PickableStatuses.Contains(item.Order.Status))
            .ToListAsync(cancellationToken);

        return Ok(BuildPickingList(items));
    }

    [HttpGet("fulfillment/orders/{orderId:guid}/picking-list")]
    public async Task<ActionResult<IReadOnlyList<PickingListItemResponse>>> GetPickingListByOrder(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var orderExists = await dbContext.Orders
            .AsNoTracking()
            .AnyAsync(order => order.Id == orderId, cancellationToken);

        if (!orderExists)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        var items = await dbContext.OrderItems
            .AsNoTracking()
            .Include(item => item.Order)
            .Include(item => item.Product)
            .ThenInclude(product => product.InventoryItem)
            .Include(item => item.ProductVariant)
            .Where(item =>
                item.OrderId == orderId &&
                PickableStatuses.Contains(item.Order.Status))
            .ToListAsync(cancellationToken);

        return Ok(BuildPickingList(items));
    }

    [HttpGet("exports/orders.csv")]
    public async Task<IActionResult> ExportOrdersCsv(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.User)
            .Include(order => order.Items)
            .Include(order => order.Payments)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(order => order.CreatedAt >= from.Value.Date);
        }

        if (to.HasValue)
        {
            query = query.Where(order => order.CreatedAt < to.Value.Date.AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Order status is invalid."));
            }

            query = query.Where(order => order.Status == parsedStatus);
        }

        var orders = await query
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("OrderId,CustomerEmail,Status,PaymentStatus,Subtotal,Discount,Shipping,Total,ItemsCount,CreatedAt,PaidAt,ShippedAt,DeliveredAt");

        foreach (var order in orders)
        {
            var latestPaymentStatus = order.Payments
                .OrderByDescending(payment => payment.CreatedAt)
                .Select(payment => payment.Status.ToString())
                .FirstOrDefault() ?? string.Empty;

            csv.AppendLine(string.Join(',', new[]
            {
                Escape(order.Id.ToString()),
                Escape(order.User.Email ?? string.Empty),
                Escape(order.Status.ToString()),
                Escape(latestPaymentStatus),
                Escape(order.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(order.DiscountAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(order.ShippingFee.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(order.Total.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(order.Items.Sum(item => item.Quantity).ToString(CultureInfo.InvariantCulture)),
                Escape(ToCsvDate(order.CreatedAt)),
                Escape(ToCsvDate(order.PaidAt)),
                Escape(ToCsvDate(order.ShippedAt)),
                Escape(ToCsvDate(order.DeliveredAt))
            }));
        }

        return File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv",
            "matger-orders.csv");
    }

    private static IReadOnlyList<PickingListItemResponse> BuildPickingList(IReadOnlyList<OrderItem> items)
    {
        return items
            .GroupBy(item => new
            {
                item.ProductId,
                item.ProductVariantId
            })
            .Select(group =>
            {
                var first = group.First();
                var variant = first.ProductVariant;
                var inventoryItem = first.Product.InventoryItem;
                var available = variant?.QuantityAvailable ?? inventoryItem?.QuantityAvailable ?? 0;
                var reserved = variant?.QuantityReserved ?? inventoryItem?.QuantityReserved ?? 0;
                var toPick = group.Sum(item => item.Quantity);

                return new PickingListItemResponse
                {
                    SKU = variant?.SKU ?? first.ProductSkuSnapshot,
                    ProductId = first.ProductId,
                    VariantId = first.ProductVariantId,
                    ProductName = first.ProductNameSnapshot,
                    VariantName = first.VariantNameSnapshot,
                    TotalQuantityToPick = toPick,
                    NumberOfOrders = group.Select(item => item.OrderId).Distinct().Count(),
                    CurrentAvailableStock = available,
                    CurrentReservedStock = reserved,
                    StockWarning = ResolveStockWarning(available, reserved, toPick)
                };
            })
            .OrderBy(item => item.SKU)
            .ToList();
    }

    private static string ResolveStockWarning(
        int available,
        int reserved,
        int toPick)
    {
        if (available + reserved <= 0)
        {
            return "OutOfStock";
        }

        if (reserved < toPick)
        {
            return "ReservedBelowPickQuantity";
        }

        if (available <= 0)
        {
            return "NoAvailableStockAfterReservations";
        }

        return "None";
    }

    private static string ToCsvDate(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("O", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string Escape(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);

        return $"\"{escaped}\"";
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
}
