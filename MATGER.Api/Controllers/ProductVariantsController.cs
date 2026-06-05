using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.ProductVariants;
using MATGER.Api.Entities;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/products/{productId:guid}/variants")]
public sealed class ProductVariantsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ProductVariantResponse>>> GetByProduct(
        Guid productId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var canIncludeInactive = User.IsInRole(ApplicationRoles.Admin);
        includeInactive = includeInactive && canIncludeInactive;

        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(product =>
                product.Id == productId &&
                product.IsActive &&
                product.Category.IsActive,
                cancellationToken);

        if (!productExists)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        var query = dbContext.ProductVariants
            .AsNoTracking()
            .Include(variant => variant.Product)
            .Where(variant => variant.ProductId == productId);

        if (!includeInactive)
        {
            query = query.Where(variant => variant.IsActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var variants = await query
            .OrderBy(variant => variant.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(variant => ToResponse(variant))
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<ProductVariantResponse>.Create(
            variants,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("{variantId:guid}")]
    public async Task<ActionResult<ProductVariantResponse>> GetById(
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var variant = await LoadVariantAsync(productId, variantId, cancellationToken);

        if (variant is null ||
            !variant.IsActive ||
            !variant.Product.IsActive ||
            !variant.Product.Category.IsActive)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product variant was not found."));
        }

        return Ok(ToResponse(variant));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    public async Task<ActionResult<ProductVariantResponse>> Create(
        Guid productId,
        CreateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCreateRequest(request);

        if (validationError is not null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validationError));
        }

        var product = await dbContext.Products
            .Include(product => product.Category)
            .FirstOrDefaultAsync(product => product.Id == productId, cancellationToken);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        var normalizedSku = request.SKU.Trim().ToUpperInvariant();

        var skuExists = await dbContext.ProductVariants
            .AnyAsync(variant => variant.SKU == normalizedSku, cancellationToken);

        if (skuExists || await dbContext.Products.AnyAsync(other => other.SKU == normalizedSku, cancellationToken))
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Variant SKU already exists."));
        }

        var now = DateTime.UtcNow;

        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            Name = request.Name.Trim(),
            SKU = normalizedSku,
            PriceOverride = request.PriceOverride,
            IsActive = request.IsActive,
            QuantityAvailable = request.QuantityAvailable,
            QuantityReserved = 0,
            LowStockThreshold = request.LowStockThreshold,
            CreatedAt = now
        };

        dbContext.ProductVariants.Add(variant);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadVariantAsync(productId, variant.Id, cancellationToken);

        return new ObjectResult(ToResponse(response!))
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{variantId:guid}")]
    public async Task<ActionResult<ProductVariantResponse>> Update(
        Guid productId,
        Guid variantId,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var variant = await LoadVariantAsync(productId, variantId, cancellationToken);

        if (variant is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product variant was not found."));
        }

        var validationError = ValidateUpdateRequest(request);

        if (validationError is not null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validationError));
        }

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Variant name is required."));
            }

            variant.Name = request.Name.Trim();
        }

        if (request.SKU is not null)
        {
            if (string.IsNullOrWhiteSpace(request.SKU))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Variant SKU is required."));
            }

            var normalizedSku = request.SKU.Trim().ToUpperInvariant();

            var skuExists = await dbContext.ProductVariants
                .AnyAsync(other =>
                    other.Id != variant.Id &&
                    other.SKU == normalizedSku,
                    cancellationToken);

            var productSkuExists = await dbContext.Products
                .AnyAsync(product => product.SKU == normalizedSku, cancellationToken);

            if (skuExists || productSkuExists)
            {
                return Conflict(Error(
                    StatusCodes.Status409Conflict,
                    "Variant SKU already exists."));
            }

            variant.SKU = normalizedSku;
        }

        if (request.ClearPriceOverride)
        {
            variant.PriceOverride = null;
        }
        else if (request.PriceOverride.HasValue)
        {
            variant.PriceOverride = request.PriceOverride.Value;
        }

        if (request.IsActive.HasValue)
        {
            variant.IsActive = request.IsActive.Value;
        }

        if (request.QuantityAvailable.HasValue)
        {
            variant.QuantityAvailable = request.QuantityAvailable.Value;
        }

        if (request.LowStockThreshold.HasValue)
        {
            variant.LowStockThreshold = request.LowStockThreshold.Value;
        }

        variant.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(variant));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{variantId:guid}/activate")]
    public async Task<ActionResult<ProductVariantResponse>> Activate(
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        return await SetActiveStateAsync(productId, variantId, true, cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{variantId:guid}/deactivate")]
    public async Task<ActionResult<ProductVariantResponse>> Deactivate(
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        return await SetActiveStateAsync(productId, variantId, false, cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost("{variantId:guid}/stock-adjustments")]
    public async Task<ActionResult<ProductVariantResponse>> AdjustStock(
        Guid productId,
        Guid variantId,
        AdjustProductVariantStockRequest request,
        CancellationToken cancellationToken)
    {
        if (request.QuantityChange == 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Quantity change cannot be zero."));
        }

        var variant = await LoadVariantAsync(productId, variantId, cancellationToken);

        if (variant is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product variant was not found."));
        }

        var newAvailableQuantity = variant.QuantityAvailable + request.QuantityChange;

        if (newAvailableQuantity < 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Stock adjustment cannot make available quantity negative."));
        }

        variant.QuantityAvailable = newAvailableQuantity;
        variant.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(variant));
    }

    private async Task<ActionResult<ProductVariantResponse>> SetActiveStateAsync(
        Guid productId,
        Guid variantId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var variant = await LoadVariantAsync(productId, variantId, cancellationToken);

        if (variant is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product variant was not found."));
        }

        if (variant.IsActive != isActive)
        {
            variant.IsActive = isActive;
            variant.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(ToResponse(variant));
    }

    private async Task<ProductVariant?> LoadVariantAsync(
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ProductVariants
            .Include(variant => variant.Product)
            .ThenInclude(product => product.Category)
            .FirstOrDefaultAsync(variant =>
                variant.Id == variantId &&
                variant.ProductId == productId,
                cancellationToken);
    }

    private static string? ValidateCreateRequest(CreateProductVariantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Variant name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.SKU))
        {
            return "Variant SKU is required.";
        }

        if (request.PriceOverride.HasValue && request.PriceOverride.Value <= 0)
        {
            return "Variant price override must be greater than zero when provided.";
        }

        if (request.QuantityAvailable < 0)
        {
            return "Variant available quantity cannot be negative.";
        }

        if (request.LowStockThreshold < 0)
        {
            return "Variant low stock threshold cannot be negative.";
        }

        return null;
    }

    private static string? ValidateUpdateRequest(UpdateProductVariantRequest request)
    {
        if (request.ClearPriceOverride && request.PriceOverride.HasValue)
        {
            return "Cannot set and clear the variant price override in the same request.";
        }

        if (request.PriceOverride.HasValue && request.PriceOverride.Value <= 0)
        {
            return "Variant price override must be greater than zero when provided.";
        }

        if (request.QuantityAvailable.HasValue && request.QuantityAvailable.Value < 0)
        {
            return "Variant available quantity cannot be negative.";
        }

        if (request.LowStockThreshold.HasValue && request.LowStockThreshold.Value < 0)
        {
            return "Variant low stock threshold cannot be negative.";
        }

        return null;
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

    private static ProductVariantResponse ToResponse(ProductVariant variant)
    {
        var effectivePrice = variant.PriceOverride ?? variant.Product.Price;

        return new ProductVariantResponse
        {
            Id = variant.Id,
            ProductId = variant.ProductId,
            ProductName = variant.Product.Name,
            Name = variant.Name,
            SKU = variant.SKU,
            ProductPrice = variant.Product.Price,
            PriceOverride = variant.PriceOverride,
            EffectivePrice = effectivePrice,
            IsActive = variant.IsActive,
            QuantityAvailable = variant.QuantityAvailable,
            QuantityReserved = variant.QuantityReserved,
            LowStockThreshold = variant.LowStockThreshold,
            IsInStock = variant.QuantityAvailable > 0,
            IsLowStock = variant.QuantityAvailable <= variant.LowStockThreshold,
            CreatedAt = variant.CreatedAt,
            UpdatedAt = variant.UpdatedAt
        };
    }
}
