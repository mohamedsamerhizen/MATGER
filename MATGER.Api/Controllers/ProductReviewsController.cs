using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.ProductReviews;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/products/{productId:guid}/reviews")]
public sealed class ProductReviewsController(
    IProductReviewService productReviewService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ProductReviewResponse>>> GetByProduct(
        Guid productId,
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        return await productReviewService.GetByProductAsync(
            productId,
            page,
            pageSize,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [HttpPost]
    public async Task<ActionResult<ProductReviewResponse>> Create(
        Guid productId,
        CreateProductReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await productReviewService.CreateAsync(
            productId,
            request,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [HttpPatch("{reviewId:guid}")]
    public async Task<ActionResult<ProductReviewResponse>> Update(
        Guid productId,
        Guid reviewId,
        UpdateProductReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await productReviewService.UpdateAsync(
            productId,
            reviewId,
            request,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [HttpDelete("{reviewId:guid}")]
    public async Task<IActionResult> Delete(
        Guid productId,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await productReviewService.DeleteAsync(
            productId,
            reviewId,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost("{reviewId:guid}/hide")]
    public async Task<ActionResult<ProductReviewResponse>> Hide(
        Guid productId,
        Guid reviewId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ModerateProductReviewRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await productReviewService.HideAsync(
            productId,
            reviewId,
            request,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost("{reviewId:guid}/show")]
    public async Task<ActionResult<ProductReviewResponse>> Show(
        Guid productId,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await productReviewService.ShowAsync(
            productId,
            reviewId,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
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
