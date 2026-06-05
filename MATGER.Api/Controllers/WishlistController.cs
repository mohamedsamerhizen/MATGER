using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Wishlist;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/wishlist")]
[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
public sealed class WishlistController(
    IWishlistService wishlistService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<WishlistItemResponse>>> GetMyWishlist(
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        return await wishlistService.GetMyWishlistAsync(
            userId.Value,
            page,
            pageSize,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [HttpPost("{productId:guid}")]
    public async Task<ActionResult<WishlistItemResponse>> Add(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await wishlistService.AddAsync(
            productId,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> Remove(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await wishlistService.RemoveAsync(
            productId,
            userId.Value,
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
