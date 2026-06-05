using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.ProductReviews;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class ProductReviewService(
    ApplicationDbContext dbContext,
    IAuditLogService auditLogService) : IProductReviewService
{
    public async Task<ActionResult<PaginatedResponse<ProductReviewResponse>>> GetByProductAsync(
        Guid productId,
        int page,
        int pageSize,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(product =>
                product.Id == productId &&
                product.IsActive &&
                product.Category.IsActive,
                cancellationToken);

        if (!productExists)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Product was not found.",
                traceId));
        }

        var query = dbContext.ProductReviews
            .AsNoTracking()
            .Include(review => review.Product)
            .Include(review => review.User)
            .Where(review =>
                review.ProductId == productId &&
                review.Status == ProductReviewStatus.Visible)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var reviews = await query
            .OrderByDescending(review => review.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(review => ToResponse(review))
            .ToListAsync(cancellationToken);

        return new OkObjectResult(PaginatedResponse<ProductReviewResponse>.Create(
            reviews,
            page,
            pageSize,
            totalCount));
    }

    public async Task<ActionResult<ProductReviewResponse>> CreateAsync(
        Guid productId,
        CreateProductReviewRequest request,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRatingAndComment(request.Rating, request.Comment);

        if (validationError is not null)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                validationError,
                traceId));
        }

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

        var alreadyReviewed = await dbContext.ProductReviews
            .AnyAsync(review =>
                review.ProductId == productId &&
                review.UserId == userId,
                cancellationToken);

        if (alreadyReviewed)
        {
            return new ConflictObjectResult(Error(
                StatusCodes.Status409Conflict,
                "Product was already reviewed by this customer.",
                traceId));
        }

        var deliveredOrder = await dbContext.Orders
            .Where(order =>
                order.UserId == userId &&
                order.Status == OrderStatus.Delivered &&
                order.Items.Any(item => item.ProductId == productId))
            .OrderByDescending(order => order.DeliveredAt ?? order.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (deliveredOrder is null)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "You can review only products from delivered orders.",
                traceId));
        }

        var now = DateTime.UtcNow;
        var review = new ProductReview
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = product.Id,
            Product = product,
            OrderId = deliveredOrder.Id,
            Order = deliveredOrder,
            Rating = request.Rating,
            Comment = NormalizeOptional(request.Comment),
            Status = ProductReviewStatus.Pending,
            CreatedAt = now
        };

        dbContext.ProductReviews.Add(review);

        await auditLogService.LogAsync(
            actorUserId: userId,
            action: "ProductReviewCreated",
            entityName: nameof(ProductReview),
            entityId: review.Id.ToString(),
            oldValue: null,
            newValue: new
            {
                review.Id,
                review.ProductId,
                ProductName = product.Name,
                review.OrderId,
                review.Rating,
                review.Comment,
                Status = review.Status.ToString(),
                review.CreatedAt
            },
            reason: "Customer submitted a product review for moderation.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var createdReview = await LoadReviewAsync(review.Id, cancellationToken);

        return new ObjectResult(ToResponse(createdReview!))
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    public async Task<ActionResult<ProductReviewResponse>> UpdateAsync(
        Guid productId,
        Guid reviewId,
        UpdateProductReviewRequest request,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!request.Rating.HasValue && request.Comment is null)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "At least one review field must be provided.",
                traceId));
        }

        var validationError = ValidateRatingAndComment(request.Rating, request.Comment);

        if (validationError is not null)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                validationError,
                traceId));
        }

        var review = await LoadReviewAsync(reviewId, cancellationToken);

        if (review is null || review.ProductId != productId)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Product review was not found.",
                traceId));
        }

        if (review.UserId != userId)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Product review was not found.",
                traceId));
        }

        var oldValue = ToAuditSnapshot(review);

        if (request.Rating.HasValue)
        {
            review.Rating = request.Rating.Value;
        }

        if (request.Comment is not null)
        {
            review.Comment = NormalizeOptional(request.Comment);
        }

        review.UpdatedAt = DateTime.UtcNow;

        await auditLogService.LogAsync(
            actorUserId: userId,
            action: "ProductReviewUpdated",
            entityName: nameof(ProductReview),
            entityId: review.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(review),
            reason: "Customer updated a product review.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(review));
    }

    public async Task<IActionResult> DeleteAsync(
        Guid productId,
        Guid reviewId,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var review = await LoadReviewAsync(reviewId, cancellationToken);

        if (review is null || review.ProductId != productId || review.UserId != userId)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Product review was not found.",
                traceId));
        }

        await auditLogService.LogAsync(
            actorUserId: userId,
            action: "ProductReviewDeleted",
            entityName: nameof(ProductReview),
            entityId: review.Id.ToString(),
            oldValue: ToAuditSnapshot(review),
            newValue: null,
            reason: "Customer deleted a product review.",
            cancellationToken: cancellationToken);

        dbContext.ProductReviews.Remove(review);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new NoContentResult();
    }

    public async Task<ActionResult<ProductReviewResponse>> HideAsync(
        Guid productId,
        Guid reviewId,
        ModerateProductReviewRequest? request,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var review = await LoadReviewAsync(reviewId, cancellationToken);

        if (review is null || review.ProductId != productId)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Product review was not found.",
                traceId));
        }

        var oldValue = ToAuditSnapshot(review);

        review.Status = ProductReviewStatus.Hidden;
        review.HiddenAt = DateTime.UtcNow;
        review.HiddenByUserId = actorUserId;
        review.AdminNote = NormalizeOptional(request?.AdminNote);
        review.UpdatedAt = DateTime.UtcNow;

        await auditLogService.LogAsync(
            actorUserId: actorUserId,
            action: "ProductReviewHidden",
            entityName: nameof(ProductReview),
            entityId: review.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(review),
            reason: "Admin hid a product review.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(review));
    }

    public async Task<ActionResult<ProductReviewResponse>> ShowAsync(
        Guid productId,
        Guid reviewId,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var review = await LoadReviewAsync(reviewId, cancellationToken);

        if (review is null || review.ProductId != productId)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Product review was not found.",
                traceId));
        }

        var oldValue = ToAuditSnapshot(review);

        review.Status = ProductReviewStatus.Visible;
        review.HiddenAt = null;
        review.HiddenByUserId = null;
        review.AdminNote = null;
        review.UpdatedAt = DateTime.UtcNow;

        await auditLogService.LogAsync(
            actorUserId: actorUserId,
            action: "ProductReviewShown",
            entityName: nameof(ProductReview),
            entityId: review.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(review),
            reason: "Admin restored a product review.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(review));
    }

    private async Task<ProductReview?> LoadReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ProductReviews
            .Include(review => review.Product)
            .Include(review => review.User)
            .FirstOrDefaultAsync(review => review.Id == reviewId, cancellationToken);
    }

    private static string? ValidateRatingAndComment(int rating, string? comment)
    {
        return ValidateRatingAndComment((int?)rating, comment);
    }

    private static string? ValidateRatingAndComment(int? rating, string? comment)
    {
        if (rating.HasValue && (rating.Value < 1 || rating.Value > 5))
        {
            return "Rating must be between 1 and 5.";
        }

        if (comment is not null && comment.Length > 1000)
        {
            return "Review comment cannot exceed 1000 characters.";
        }

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    private static ProductReviewResponse ToResponse(ProductReview review)
    {
        return new ProductReviewResponse
        {
            Id = review.Id,
            ProductId = review.ProductId,
            ProductName = review.Product.Name,
            UserId = review.UserId,
            UserFullName = review.User.FullName,
            OrderId = review.OrderId,
            Rating = review.Rating,
            Comment = review.Comment,
            Status = review.Status.ToString(),
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt,
            HiddenAt = review.HiddenAt,
            AdminNote = review.AdminNote
        };
    }

    private static object ToAuditSnapshot(ProductReview review)
    {
        return new
        {
            review.Id,
            review.ProductId,
            ProductName = review.Product.Name,
            review.UserId,
            review.OrderId,
            review.Rating,
            review.Comment,
            Status = review.Status.ToString(),
            review.CreatedAt,
            review.UpdatedAt,
            review.HiddenAt,
            review.HiddenByUserId,
            review.AdminNote
        };
    }
}
