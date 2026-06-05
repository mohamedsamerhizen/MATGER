using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Inventory;
using MATGER.Api.Helpers;
using MATGER.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class InventoryIntelligenceService(ApplicationDbContext dbContext) : IInventoryIntelligenceService
{
    public async Task<InventoryHealthSummaryResponse> GetHealthSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var inventory = await dbContext.InventoryItems
            .AsNoTracking()
            .Select(item => new
            {
                item.QuantityAvailable,
                item.QuantityReserved,
                item.LowStockThreshold,
                ProductIsActive = item.Product.IsActive,
                ProductPrice = item.Product.Price
            })
            .ToListAsync(cancellationToken);

        return new InventoryHealthSummaryResponse
        {
            TotalInventoryItems = inventory.Count,
            ActiveProducts = inventory.Count(item => item.ProductIsActive),
            InStockProducts = inventory.Count(item => item.QuantityAvailable > 0),
            LowStockProducts = inventory.Count(item => item.QuantityAvailable <= item.LowStockThreshold),
            OutOfStockProducts = inventory.Count(item => item.QuantityAvailable <= 0),
            ReservedProducts = inventory.Count(item => item.QuantityReserved > 0),
            NegativeStockProducts = inventory.Count(item => item.QuantityAvailable < 0 || item.QuantityReserved < 0),
            TotalQuantityAvailable = inventory.Sum(item => item.QuantityAvailable),
            TotalQuantityReserved = inventory.Sum(item => item.QuantityReserved),
            TotalQuantityOnHand = inventory.Sum(item => item.QuantityAvailable + item.QuantityReserved),
            TotalAvailableInventoryValue = inventory.Sum(item => item.QuantityAvailable * item.ProductPrice),
            TotalOnHandInventoryValue = inventory.Sum(item => (item.QuantityAvailable + item.QuantityReserved) * item.ProductPrice),
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<PaginatedResponse<InventoryAttentionItemResponse>> GetNeedsAttentionAsync(
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.InventoryItems
            .AsNoTracking()
            .AsQueryable();

        query = status switch
        {
            InventoryAttentionStatuses.LowStock => query.Where(item => item.QuantityAvailable <= item.LowStockThreshold),
            InventoryAttentionStatuses.OutOfStock => query.Where(item => item.QuantityAvailable <= 0),
            InventoryAttentionStatuses.Reserved => query.Where(item => item.QuantityReserved > 0),
            InventoryAttentionStatuses.NegativeStock => query.Where(item => item.QuantityAvailable < 0 || item.QuantityReserved < 0),
            InventoryAttentionStatuses.Healthy => query.Where(item =>
                item.QuantityAvailable > item.LowStockThreshold &&
                item.QuantityReserved == 0 &&
                item.QuantityAvailable >= 0),
            _ => query.Where(item =>
                item.QuantityAvailable <= item.LowStockThreshold ||
                item.QuantityReserved > 0 ||
                item.QuantityAvailable < 0 ||
                item.QuantityReserved < 0)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(item => item.QuantityAvailable <= 0 ? 0 : 1)
            .ThenBy(item => item.QuantityAvailable <= item.LowStockThreshold ? 0 : 1)
            .ThenBy(item => item.QuantityAvailable)
            .ThenByDescending(item => item.QuantityReserved)
            .ThenBy(item => item.Product.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new InventoryAttentionItemResponse
            {
                InventoryItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                ProductSku = item.Product.SKU,
                ProductPrice = item.Product.Price,
                CategoryId = item.Product.CategoryId,
                CategoryName = item.Product.Category.Name,
                QuantityAvailable = item.QuantityAvailable,
                QuantityReserved = item.QuantityReserved,
                QuantityOnHand = item.QuantityAvailable + item.QuantityReserved,
                LowStockThreshold = item.LowStockThreshold,
                IsOutOfStock = item.QuantityAvailable <= 0,
                IsLowStock = item.QuantityAvailable <= item.LowStockThreshold,
                HasReservedQuantity = item.QuantityReserved > 0,
                HasNegativeStock = item.QuantityAvailable < 0 || item.QuantityReserved < 0,
                RestockSuggestedQuantity = item.QuantityAvailable < item.LowStockThreshold
                    ? item.LowStockThreshold - item.QuantityAvailable
                    : 0,
                EstimatedAvailableValue = item.QuantityAvailable * item.Product.Price,
                EstimatedOnHandValue = (item.QuantityAvailable + item.QuantityReserved) * item.Product.Price,
                Severity = item.QuantityAvailable < 0 || item.QuantityReserved < 0
                    ? "NegativeStock"
                    : item.QuantityAvailable <= 0
                        ? "OutOfStock"
                        : item.QuantityAvailable <= item.LowStockThreshold
                            ? "LowStock"
                            : item.QuantityReserved > 0
                                ? "Reserved"
                                : "Healthy",
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return PaginatedResponse<InventoryAttentionItemResponse>.Create(
            items,
            page,
            pageSize,
            totalCount);
    }

    public async Task<PaginatedResponse<TopReservedProductResponse>> GetTopReservedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => item.QuantityReserved > 0);

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(item => item.QuantityReserved)
            .ThenBy(item => item.QuantityAvailable)
            .ThenBy(item => item.Product.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                InventoryItemId = item.Id,
                item.ProductId,
                ProductName = item.Product.Name,
                ProductSku = item.Product.SKU,
                item.Product.CategoryId,
                CategoryName = item.Product.Category.Name,
                item.QuantityAvailable,
                item.QuantityReserved,
                item.CreatedAt,
                item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(item =>
            {
                var quantityOnHand = item.QuantityAvailable + item.QuantityReserved;
                var reservedSharePercentage = quantityOnHand == 0
                    ? 0m
                    : Math.Round((decimal)item.QuantityReserved / quantityOnHand * 100m, 2);

                return new TopReservedProductResponse
                {
                    InventoryItemId = item.InventoryItemId,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    ProductSku = item.ProductSku,
                    CategoryId = item.CategoryId,
                    CategoryName = item.CategoryName,
                    QuantityAvailable = item.QuantityAvailable,
                    QuantityReserved = item.QuantityReserved,
                    QuantityOnHand = quantityOnHand,
                    ReservedSharePercentage = reservedSharePercentage,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt
                };
            })
            .ToList();

        return PaginatedResponse<TopReservedProductResponse>.Create(
            items,
            page,
            pageSize,
            totalCount);
    }
}

public static class InventoryAttentionStatuses
{
    public const string All = "all";
    public const string LowStock = "low-stock";
    public const string OutOfStock = "out-of-stock";
    public const string Reserved = "reserved";
    public const string NegativeStock = "negative-stock";
    public const string Healthy = "healthy";

    public static readonly string[] Allowed =
    [
        All,
        LowStock,
        OutOfStock,
        Reserved,
        NegativeStock,
        Healthy
    ];
}
