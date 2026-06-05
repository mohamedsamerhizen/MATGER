using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Products;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ProductResponse>>> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? categorySlug = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] bool inStockOnly = false,
        [FromQuery] bool featuredOnly = false,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateProductSearchRequest(
            categoryId,
            minPrice,
            maxPrice,
            sortBy);

        if (validationError is not null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validationError));
        }

        var normalizedSortBy = NormalizeSortBy(sortBy) ?? "name";
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.Category.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var normalizedSkuSearch = normalizedSearch.ToUpperInvariant();

            query = query.Where(product =>
                product.Name.Contains(normalizedSearch) ||
                product.Description.Contains(normalizedSearch) ||
                product.SKU.Contains(normalizedSkuSearch));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(product => product.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            var normalizedCategorySlug = categorySlug.Trim().ToLowerInvariant();

            query = query.Where(product => product.Category.Slug == normalizedCategorySlug);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(product => product.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(product => product.Price <= maxPrice.Value);
        }

        if (inStockOnly)
        {
            query = query.Where(product =>
                product.InventoryItem != null &&
                product.InventoryItem.QuantityAvailable > 0);
        }

        if (featuredOnly)
        {
            query = query.Where(product => product.IsFeatured);
        }

        query = ApplySorting(query, normalizedSortBy);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await SelectToResponse(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<ProductResponse>.Create(
            products,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("featured")]
    public async Task<ActionResult<PaginatedResponse<ProductResponse>>> GetFeatured(
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.Products
            .AsNoTracking()
            .Where(product =>
                product.IsActive &&
                product.IsFeatured &&
                product.Category.IsActive)
            .OrderByDescending(product => product.CreatedAt)
            .ThenBy(product => product.Name);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await SelectToResponse(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<ProductResponse>.Create(
            products,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("new-arrivals")]
    public async Task<ActionResult<PaginatedResponse<ProductResponse>>> GetNewArrivals(
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.Category.IsActive)
            .OrderByDescending(product => product.CreatedAt)
            .ThenBy(product => product.Name);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await SelectToResponse(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<ProductResponse>.Create(
            products,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await SelectToResponse(
                dbContext.Products
                    .AsNoTracking()
                    .Where(product =>
                        product.Id == id &&
                        product.IsActive &&
                        product.Category.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        return Ok(product);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCreateRequest(request);

        if (validationError is not null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validationError));
        }

        var normalizedSku = request.SKU.Trim().ToUpperInvariant();

        var skuExists = await dbContext.Products
                            .AnyAsync(product => product.SKU == normalizedSku, cancellationToken) ||
                        await dbContext.ProductVariants
                            .AnyAsync(variant => variant.SKU == normalizedSku, cancellationToken);

        if (skuExists)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Product SKU already exists."));
        }

        var category = await dbContext.Categories
            .FirstOrDefaultAsync(category =>
                category.Id == request.CategoryId &&
                category.IsActive,
                cancellationToken);

        if (category is null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Active category was not found."));
        }

        var now = DateTime.UtcNow;

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            SKU = normalizedSku,
            Price = request.Price,
            IsActive = true,
            IsFeatured = request.IsFeatured,
            WeightKg = request.WeightKg,
            LengthCm = request.LengthCm,
            WidthCm = request.WidthCm,
            HeightCm = request.HeightCm,
            IsReturnable = request.IsReturnable,
            ReturnWindowDays = request.ReturnWindowDays,
            CategoryId = category.Id,
            Category = category,
            CreatedAt = now
        };

        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            QuantityAvailable = 0,
            QuantityReserved = 0,
            LowStockThreshold = 5,
            CreatedAt = now
        };

        product.InventoryItem = inventoryItem;

        dbContext.Products.Add(product);
        dbContext.InventoryItems.Add(inventoryItem);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToResponse(
            product,
            category,
            new ProductSummary(
                AverageRating: 0m,
                ReviewsCount: 0,
                QuantityAvailable: 0,
                QuantityReserved: 0,
                LowStockThreshold: inventoryItem.LowStockThreshold,
                ActiveVariantsCount: 0));

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            response);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(product => product.Category)
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product name is required."));
            }

            product.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product description is required."));
            }

            product.Description = request.Description.Trim();
        }

        if (request.SKU is not null)
        {
            if (string.IsNullOrWhiteSpace(request.SKU))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product SKU is required."));
            }

            var normalizedSku = request.SKU.Trim().ToUpperInvariant();

            var skuExists = await dbContext.Products
                                .AnyAsync(otherProduct =>
                                    otherProduct.Id != id &&
                                    otherProduct.SKU == normalizedSku,
                                    cancellationToken) ||
                            await dbContext.ProductVariants
                                .AnyAsync(variant => variant.SKU == normalizedSku, cancellationToken);

            if (skuExists)
            {
                return Conflict(Error(
                    StatusCodes.Status409Conflict,
                    "Product SKU already exists."));
            }

            product.SKU = normalizedSku;
        }

        if (request.Price.HasValue)
        {
            if (request.Price.Value <= 0)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product price must be greater than zero."));
            }

            product.Price = request.Price.Value;
        }

        if (request.CategoryId.HasValue)
        {
            if (request.CategoryId.Value == Guid.Empty)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Category id is required."));
            }

            var category = await dbContext.Categories
                .FirstOrDefaultAsync(category =>
                    category.Id == request.CategoryId.Value &&
                    category.IsActive,
                    cancellationToken);

            if (category is null)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Active category was not found."));
            }

            product.CategoryId = category.Id;
            product.Category = category;
        }

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value && !product.Category.IsActive)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product cannot be activated while its category is inactive."));
            }

            product.IsActive = request.IsActive.Value;
        }

        if (request.IsFeatured.HasValue)
        {
            product.IsFeatured = request.IsFeatured.Value;
        }

        if (request.WeightKg.HasValue)
        {
            if (request.WeightKg.Value <= 0)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product weight must be greater than zero when provided."));
            }

            product.WeightKg = request.WeightKg.Value;
        }

        if (request.LengthCm.HasValue)
        {
            if (request.LengthCm.Value <= 0)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product length must be greater than zero when provided."));
            }

            product.LengthCm = request.LengthCm.Value;
        }

        if (request.WidthCm.HasValue)
        {
            if (request.WidthCm.Value <= 0)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product width must be greater than zero when provided."));
            }

            product.WidthCm = request.WidthCm.Value;
        }

        if (request.HeightCm.HasValue)
        {
            if (request.HeightCm.Value <= 0)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product height must be greater than zero when provided."));
            }

            product.HeightCm = request.HeightCm.Value;
        }

        if (request.IsReturnable.HasValue)
        {
            product.IsReturnable = request.IsReturnable.Value;
        }

        if (request.ReturnWindowDays.HasValue)
        {
            if (request.ReturnWindowDays.Value < 1)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product return window days must be at least 1."));
            }

            product.ReturnWindowDays = request.ReturnWindowDays.Value;
        }

        product.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var summary = await GetProductSummaryAsync(product.Id, cancellationToken);
        var response = ToResponse(
            product,
            product.Category,
            summary);

        return Ok(response);
    }


    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/activate")]
    public async Task<ActionResult<ProductResponse>> Activate(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await SetProductActiveStateAsync(id, isActive: true, cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/deactivate")]
    public async Task<ActionResult<ProductResponse>> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await SetProductActiveStateAsync(id, isActive: false, cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/feature")]
    public async Task<ActionResult<ProductResponse>> Feature(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await SetProductFeaturedStateAsync(id, isFeatured: true, cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/unfeature")]
    public async Task<ActionResult<ProductResponse>> Unfeature(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await SetProductFeaturedStateAsync(id, isFeatured: false, cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("bulk/status")]
    public async Task<ActionResult<BulkProductOperationResponse>> BulkUpdateStatus(
        BulkProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        var productIds = NormalizeProductIds(request.ProductIds);

        if (productIds.Count == 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "At least one product id is required."));
        }

        if (productIds.Any(id => id == Guid.Empty))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product ids cannot contain empty values."));
        }

        var products = await dbContext.Products
            .Include(product => product.Category)
            .Where(product => productIds.Contains(product.Id))
            .ToListAsync(cancellationToken);

        if (request.IsActive)
        {
            var inactiveCategoryProductIds = products
                .Where(product => !product.Category.IsActive)
                .Select(product => product.Id)
                .ToList();

            if (inactiveCategoryProductIds.Count > 0)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Cannot activate products that belong to inactive categories."));
            }
        }

        var updatedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var product in products)
        {
            if (product.IsActive == request.IsActive)
            {
                continue;
            }

            product.IsActive = request.IsActive;

            if (!request.IsActive)
            {
                product.IsFeatured = false;
            }

            product.UpdatedAt = now;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var responseProducts = await GetProductsResponseByIdsAsync(productIds, cancellationToken);

        return Ok(new BulkProductOperationResponse
        {
            RequestedCount = productIds.Count,
            MatchedCount = products.Count,
            UpdatedCount = updatedCount,
            AlreadyMatchingCount = products.Count - updatedCount,
            NotFoundProductIds = productIds
                .Except(products.Select(product => product.Id))
                .ToList(),
            Products = responseProducts
        });
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("bulk/featured")]
    public async Task<ActionResult<BulkProductOperationResponse>> BulkUpdateFeatured(
        BulkProductFeaturedRequest request,
        CancellationToken cancellationToken)
    {
        var productIds = NormalizeProductIds(request.ProductIds);

        if (productIds.Count == 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "At least one product id is required."));
        }

        if (productIds.Any(id => id == Guid.Empty))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product ids cannot contain empty values."));
        }

        var products = await dbContext.Products
            .Include(product => product.Category)
            .Where(product => productIds.Contains(product.Id))
            .ToListAsync(cancellationToken);

        if (request.IsFeatured)
        {
            var invalidProductIds = products
                .Where(product => !product.IsActive || !product.Category.IsActive)
                .Select(product => product.Id)
                .ToList();

            if (invalidProductIds.Count > 0)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Only active products under active categories can be featured."));
            }
        }

        var updatedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var product in products)
        {
            if (product.IsFeatured == request.IsFeatured)
            {
                continue;
            }

            product.IsFeatured = request.IsFeatured;
            product.UpdatedAt = now;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var responseProducts = await GetProductsResponseByIdsAsync(productIds, cancellationToken);

        return Ok(new BulkProductOperationResponse
        {
            RequestedCount = productIds.Count,
            MatchedCount = products.Count,
            UpdatedCount = updatedCount,
            AlreadyMatchingCount = products.Count - updatedCount,
            NotFoundProductIds = productIds
                .Except(products.Select(product => product.Id))
                .ToList(),
            Products = responseProducts
        });
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/disable")]
    public async Task<IActionResult> Disable(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        if (!product.IsActive)
        {
            return NoContent();
        }

        product.IsActive = false;
        product.IsFeatured = false;
        product.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }


    private async Task<ActionResult<ProductResponse>> SetProductActiveStateAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(product => product.Category)
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        if (isActive && !product.Category.IsActive)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product cannot be activated while its category is inactive."));
        }

        if (product.IsActive != isActive)
        {
            product.IsActive = isActive;

            if (!isActive)
            {
                product.IsFeatured = false;
            }

            product.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var summary = await GetProductSummaryAsync(product.Id, cancellationToken);

        return Ok(ToResponse(
            product,
            product.Category,
            summary));
    }

    private async Task<ActionResult<ProductResponse>> SetProductFeaturedStateAsync(
        Guid id,
        bool isFeatured,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(product => product.Category)
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        if (isFeatured && (!product.IsActive || !product.Category.IsActive))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Only active products under active categories can be featured."));
        }

        if (product.IsFeatured != isFeatured)
        {
            product.IsFeatured = isFeatured;
            product.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var summary = await GetProductSummaryAsync(product.Id, cancellationToken);

        return Ok(ToResponse(
            product,
            product.Category,
            summary));
    }

    private async Task<IReadOnlyList<ProductResponse>> GetProductsResponseByIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        return await SelectToResponse(
                dbContext.Products
                    .AsNoTracking()
                    .Where(product => productIds.Contains(product.Id)))
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);
    }

    private static List<Guid> NormalizeProductIds(IReadOnlyList<Guid>? productIds)
    {
        return productIds is null
            ? []
            : productIds
                .Distinct()
                .ToList();
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

    private static string? ValidateCreateRequest(CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Product name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return "Product description is required.";
        }

        if (string.IsNullOrWhiteSpace(request.SKU))
        {
            return "Product SKU is required.";
        }

        if (request.Price <= 0)
        {
            return "Product price must be greater than zero.";
        }

        if (request.ReturnWindowDays < 1)
        {
            return "Product return window days must be at least 1.";
        }

        if (request.CategoryId == Guid.Empty)
        {
            return "Category id is required.";
        }

        return ValidateProductMeasurements(
            request.WeightKg,
            request.LengthCm,
            request.WidthCm,
            request.HeightCm);
    }

    private static string? ValidateProductMeasurements(
        decimal? weightKg,
        decimal? lengthCm,
        decimal? widthCm,
        decimal? heightCm)
    {
        if (weightKg.HasValue && weightKg.Value <= 0)
        {
            return "Product weight must be greater than zero when provided.";
        }

        if (lengthCm.HasValue && lengthCm.Value <= 0)
        {
            return "Product length must be greater than zero when provided.";
        }

        if (widthCm.HasValue && widthCm.Value <= 0)
        {
            return "Product width must be greater than zero when provided.";
        }

        if (heightCm.HasValue && heightCm.Value <= 0)
        {
            return "Product height must be greater than zero when provided.";
        }

        return null;
    }

    private static string? ValidateProductSearchRequest(
        Guid? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        string? sortBy)
    {
        if (categoryId == Guid.Empty)
        {
            return "Category id is invalid.";
        }

        if (minPrice.HasValue && minPrice.Value < 0)
        {
            return "Minimum price cannot be negative.";
        }

        if (maxPrice.HasValue && maxPrice.Value < 0)
        {
            return "Maximum price cannot be negative.";
        }

        if (minPrice.HasValue && maxPrice.HasValue && minPrice.Value > maxPrice.Value)
        {
            return "Minimum price cannot be greater than maximum price.";
        }

        if (NormalizeSortBy(sortBy) is null)
        {
            return "Sort value is invalid. Allowed values: name, newest, featured, price_asc, price_desc, rating, reviews.";
        }

        return null;
    }

    private static string? NormalizeSortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return "name";
        }

        var normalizedSortBy = sortBy
            .Trim()
            .Replace("-", "_", StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalizedSortBy switch
        {
            "name" => "name",
            "newest" => "newest",
            "created_desc" => "newest",
            "featured" => "featured",
            "featured_desc" => "featured",
            "price_asc" => "price_asc",
            "price_low_to_high" => "price_asc",
            "price_desc" => "price_desc",
            "price_high_to_low" => "price_desc",
            "rating" => "rating",
            "rating_desc" => "rating",
            "reviews" => "reviews",
            "reviews_desc" => "reviews",
            _ => null
        };
    }

    private static IQueryable<Product> ApplySorting(
        IQueryable<Product> query,
        string sortBy)
    {
        return sortBy switch
        {
            "newest" => query
                .OrderByDescending(product => product.CreatedAt)
                .ThenBy(product => product.Name),

            "featured" => query
                .OrderByDescending(product => product.IsFeatured)
                .ThenByDescending(product => product.CreatedAt)
                .ThenBy(product => product.Name),

            "price_asc" => query
                .OrderBy(product => product.Price)
                .ThenBy(product => product.Name),

            "price_desc" => query
                .OrderByDescending(product => product.Price)
                .ThenBy(product => product.Name),

            "rating" => query
                .OrderByDescending(product => product.Reviews
                    .Where(review => review.Status == ProductReviewStatus.Visible)
                    .Select(review => (decimal?)review.Rating)
                    .Average() ?? 0m)
                .ThenByDescending(product => product.Reviews
                    .Count(review => review.Status == ProductReviewStatus.Visible))
                .ThenBy(product => product.Name),

            "reviews" => query
                .OrderByDescending(product => product.Reviews
                    .Count(review => review.Status == ProductReviewStatus.Visible))
                .ThenByDescending(product => product.Reviews
                    .Where(review => review.Status == ProductReviewStatus.Visible)
                    .Select(review => (decimal?)review.Rating)
                    .Average() ?? 0m)
                .ThenBy(product => product.Name),

            _ => query.OrderBy(product => product.Name)
        };
    }

    private static IQueryable<ProductResponse> SelectToResponse(IQueryable<Product> query)
    {
        return query.Select(product => new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            SKU = product.SKU,
            Price = product.Price,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            WeightKg = product.WeightKg,
            LengthCm = product.LengthCm,
            WidthCm = product.WidthCm,
            HeightCm = product.HeightCm,
            IsReturnable = product.IsReturnable,
            ReturnWindowDays = product.ReturnWindowDays,
            ActiveVariantsCount = product.Variants.Count(variant => variant.IsActive),
            CategoryId = product.CategoryId,
            CategoryName = product.Category.Name,
            CategorySlug = product.Category.Slug,
            QuantityAvailable = product.InventoryItem == null
                ? 0
                : product.InventoryItem.QuantityAvailable,
            QuantityReserved = product.InventoryItem == null
                ? 0
                : product.InventoryItem.QuantityReserved,
            LowStockThreshold = product.InventoryItem == null
                ? 0
                : product.InventoryItem.LowStockThreshold,
            IsInStock = product.InventoryItem != null &&
                        product.InventoryItem.QuantityAvailable > 0,
            IsLowStock = product.InventoryItem != null &&
                         product.InventoryItem.QuantityAvailable <= product.InventoryItem.LowStockThreshold,
            AverageRating = product.Reviews
                .Where(review => review.Status == ProductReviewStatus.Visible)
                .Select(review => (decimal?)review.Rating)
                .Average() ?? 0m,
            ReviewsCount = product.Reviews.Count(review => review.Status == ProductReviewStatus.Visible),
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        });
    }

    private async Task<ProductSummary> GetProductSummaryAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var visibleReviews = dbContext.ProductReviews
            .AsNoTracking()
            .Where(review =>
                review.ProductId == productId &&
                review.Status == ProductReviewStatus.Visible);

        var reviewsCount = await visibleReviews.CountAsync(cancellationToken);
        var averageRating = await visibleReviews
            .Select(review => (decimal?)review.Rating)
            .AverageAsync(cancellationToken) ?? 0m;

        var activeVariantsCount = await dbContext.ProductVariants
            .AsNoTracking()
            .CountAsync(variant =>
                variant.ProductId == productId &&
                variant.IsActive,
                cancellationToken);

        var inventorySummary = await dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => item.ProductId == productId)
            .Select(item => new
            {
                item.QuantityAvailable,
                item.QuantityReserved,
                item.LowStockThreshold
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new ProductSummary(
            AverageRating: averageRating,
            ReviewsCount: reviewsCount,
            QuantityAvailable: inventorySummary?.QuantityAvailable ?? 0,
            QuantityReserved: inventorySummary?.QuantityReserved ?? 0,
            LowStockThreshold: inventorySummary?.LowStockThreshold ?? 0,
            ActiveVariantsCount: activeVariantsCount);
    }

    private static ProductResponse ToResponse(
        Product product,
        Category category,
        ProductSummary summary)
    {
        return new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            SKU = product.SKU,
            Price = product.Price,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            WeightKg = product.WeightKg,
            LengthCm = product.LengthCm,
            WidthCm = product.WidthCm,
            HeightCm = product.HeightCm,
            IsReturnable = product.IsReturnable,
            ReturnWindowDays = product.ReturnWindowDays,
            ActiveVariantsCount = summary.ActiveVariantsCount,
            CategoryId = product.CategoryId,
            CategoryName = category.Name,
            CategorySlug = category.Slug,
            QuantityAvailable = summary.QuantityAvailable,
            QuantityReserved = summary.QuantityReserved,
            LowStockThreshold = summary.LowStockThreshold,
            IsInStock = summary.QuantityAvailable > 0,
            IsLowStock = summary.QuantityAvailable <= summary.LowStockThreshold,
            AverageRating = summary.AverageRating,
            ReviewsCount = summary.ReviewsCount,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    private sealed record ProductSummary(
        decimal AverageRating,
        int ReviewsCount,
        int QuantityAvailable,
        int QuantityReserved,
        int LowStockThreshold,
        int ActiveVariantsCount);
}
