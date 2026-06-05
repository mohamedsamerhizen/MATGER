using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Wishlist;
using MATGER.Api.Entities;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class WishlistService(ApplicationDbContext dbContext) : IWishlistService
{
    public async Task<ActionResult<PaginatedResponse<WishlistItemResponse>>> GetMyWishlistAsync(
        Guid userId,
        int page,
        int pageSize,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.WishlistItems
            .AsNoTracking()
            .Include(item => item.Product)
            .ThenInclude(product => product.Category)
            .Where(item => item.UserId == userId)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => ToResponse(item))
            .ToListAsync(cancellationToken);

        return new OkObjectResult(PaginatedResponse<WishlistItemResponse>.Create(
            items,
            page,
            pageSize,
            totalCount));
    }

    public async Task<ActionResult<WishlistItemResponse>> AddAsync(
        Guid productId,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .Include(product => product.Category)
            .FirstOrDefaultAsync(product =>
                product.Id == productId &&
                product.IsActive &&
                product.Category.IsActive,
                cancellationToken);

        if (product is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Product was not found.",
                traceId));
        }

        var existingItem = await dbContext.WishlistItems
            .Include(item => item.Product)
            .ThenInclude(existingProduct => existingProduct.Category)
            .FirstOrDefaultAsync(item =>
                item.UserId == userId &&
                item.ProductId == productId,
                cancellationToken);

        if (existingItem is not null)
        {
            return new OkObjectResult(ToResponse(existingItem));
        }

        var item = new WishlistItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = product.Id,
            Product = product,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.WishlistItems.Add(item);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ObjectResult(ToResponse(item))
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    public async Task<IActionResult> RemoveAsync(
        Guid productId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.WishlistItems
            .FirstOrDefaultAsync(item =>
                item.UserId == userId &&
                item.ProductId == productId,
                cancellationToken);

        if (item is null)
        {
            return new NoContentResult();
        }

        dbContext.WishlistItems.Remove(item);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new NoContentResult();
    }

    private static ApiErrorResponse Error(int statusCode, string message, string traceId)
    {
        return new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = traceId
        };
    }

    private static WishlistItemResponse ToResponse(WishlistItem item)
    {
        return new WishlistItemResponse
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductName = item.Product.Name,
            ProductSku = item.Product.SKU,
            ProductPrice = item.Product.Price,
            CategoryId = item.Product.CategoryId,
            CategoryName = item.Product.Category.Name,
            CreatedAt = item.CreatedAt
        };
    }
}
