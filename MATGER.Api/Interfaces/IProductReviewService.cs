using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.ProductReviews;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Interfaces;

public interface IProductReviewService
{
    Task<ActionResult<PaginatedResponse<ProductReviewResponse>>> GetByProductAsync(
        Guid productId,
        int page,
        int pageSize,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ProductReviewResponse>> CreateAsync(
        Guid productId,
        CreateProductReviewRequest request,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ProductReviewResponse>> UpdateAsync(
        Guid productId,
        Guid reviewId,
        UpdateProductReviewRequest request,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DeleteAsync(
        Guid productId,
        Guid reviewId,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ProductReviewResponse>> HideAsync(
        Guid productId,
        Guid reviewId,
        ModerateProductReviewRequest? request,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ProductReviewResponse>> ShowAsync(
        Guid productId,
        Guid reviewId,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default);
}
