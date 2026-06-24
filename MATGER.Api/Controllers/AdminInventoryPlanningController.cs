using MATGER.Api.Data;
using MATGER.Api.DTOs.Inventory;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/inventory")]
[Authorize(Policy = AuthorizationPolicies.InventoryManagerOnly)]
public sealed class AdminInventoryPlanningController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("reorder-needed")]
    public async Task<ActionResult<IReadOnlyList<ReorderNeededResponse>>> GetReorderNeeded(
        CancellationToken cancellationToken)
    {
        var rawItems = await dbContext.InventoryItems
            .AsNoTracking()
            .Where(item =>
                item.ReorderPoint.HasValue &&
                item.QuantityAvailable <= item.ReorderPoint.Value)
            .OrderBy(item => item.QuantityAvailable <= 0 ? 0 : 1)
            .ThenBy(item => item.QuantityAvailable)
            .ThenBy(item => item.Product.Name)
            .Select(item => new
            {
                item.ProductId,
                ProductName = item.Product.Name,
                ProductSku = item.Product.SKU,
                item.SupplierName,
                item.SupplierSku,
                AvailableQuantity = item.QuantityAvailable,
                ReservedQuantity = item.QuantityReserved,
                ReorderPoint = item.ReorderPoint!.Value,
                SuggestedReorderQuantity = item.ReorderQuantity ?? Math.Max(item.ReorderPoint.Value - item.QuantityAvailable, 1),
                item.LeadTimeDays,
                item.BinLocation
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(item => new ReorderNeededResponse
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                SKU = item.ProductSku,
                VariantId = null,
                VariantName = null,
                SupplierName = item.SupplierName,
                SupplierSku = item.SupplierSku,
                AvailableQuantity = item.AvailableQuantity,
                ReservedQuantity = item.ReservedQuantity,
                ReorderPoint = item.ReorderPoint,
                SuggestedReorderQuantity = item.SuggestedReorderQuantity,
                LeadTimeDays = item.LeadTimeDays,
                BinLocation = item.BinLocation,
                Severity = ResolveReorderSeverity(item.AvailableQuantity, item.ReorderPoint)
            })
            .ToList();

        return Ok(items);
    }

    private static string ResolveReorderSeverity(int availableQuantity, int reorderPoint)
    {
        if (availableQuantity <= 0)
        {
            return "Critical";
        }

        if (availableQuantity <= Math.Max(1, reorderPoint / 4))
        {
            return "Critical";
        }

        if (availableQuantity <= Math.Max(1, reorderPoint / 2))
        {
            return "High";
        }

        if (availableQuantity <= Math.Max(1, reorderPoint * 3 / 4))
        {
            return "Medium";
        }

        return "Low";
    }
}
