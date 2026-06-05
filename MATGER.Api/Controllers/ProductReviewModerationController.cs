using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.ProductReviews;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/product-reviews")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class ProductReviewModerationController(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ProductReviewResponse>>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.ProductReviews
            .AsNoTracking()
            .Include(review => review.Product)
            .Include(review => review.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ProductReviewStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
            {
                return BadRequest(Error(StatusCodes.Status400BadRequest, "Review status is invalid."));
            }

            query = query.Where(review => review.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var reviews = await query
            .OrderByDescending(review => review.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(review => ToResponse(review))
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<ProductReviewResponse>.Create(
            reviews,
            page,
            pageSize,
            totalCount));
    }

    [HttpPost("{reviewId:guid}/approve")]
    public async Task<ActionResult<ProductReviewResponse>> Approve(
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(
            reviewId,
            ProductReviewStatus.Visible,
            action: "ProductReviewApproved",
            adminNote: null,
            cancellationToken);
    }

    [HttpPost("{reviewId:guid}/reject")]
    public async Task<ActionResult<ProductReviewResponse>> Reject(
        Guid reviewId,
        RejectProductReviewRequest? request,
        CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(
            reviewId,
            ProductReviewStatus.Rejected,
            action: "ProductReviewRejected",
            adminNote: request?.AdminNote,
            cancellationToken);
    }

    private async Task<ActionResult<ProductReviewResponse>> ChangeStatusAsync(
        Guid reviewId,
        ProductReviewStatus status,
        string action,
        string? adminNote,
        CancellationToken cancellationToken)
    {
        var actorUserId = currentUserService.UserId;

        if (actorUserId is null)
        {
            return Unauthorized(Error(StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var review = await dbContext.ProductReviews
            .Include(review => review.Product)
            .Include(review => review.User)
            .FirstOrDefaultAsync(review => review.Id == reviewId, cancellationToken);

        if (review is null)
        {
            return NotFound(Error(StatusCodes.Status404NotFound, "Product review was not found."));
        }

        var oldStatus = review.Status;
        var now = DateTime.UtcNow;

        review.Status = status;
        review.AdminNote = NormalizeOptional(adminNote);
        review.UpdatedAt = now;

        if (status == ProductReviewStatus.Visible)
        {
            review.HiddenAt = null;
            review.HiddenByUserId = null;
        }

        if (status == ProductReviewStatus.Hidden || status == ProductReviewStatus.Rejected)
        {
            review.HiddenAt = status == ProductReviewStatus.Hidden ? now : review.HiddenAt;
            review.HiddenByUserId = status == ProductReviewStatus.Hidden ? actorUserId.Value : review.HiddenByUserId;
        }

        await auditLogService.LogAsync(
            actorUserId: actorUserId.Value,
            action: action,
            entityName: nameof(ProductReview),
            entityId: review.Id.ToString(),
            oldValue: new
            {
                Status = oldStatus.ToString(),
                review.AdminNote
            },
            newValue: new
            {
                Status = review.Status.ToString(),
                review.AdminNote,
                review.UpdatedAt
            },
            reason: "Admin moderated a product review.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(review));
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
}
