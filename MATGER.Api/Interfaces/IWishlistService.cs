using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Wishlist;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Interfaces;

public interface IWishlistService
{
    Task<ActionResult<PaginatedResponse<WishlistItemResponse>>> GetMyWishlistAsync(
        Guid userId,
        int page,
        int pageSize,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<WishlistItemResponse>> AddAsync(
        Guid productId,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<IActionResult> RemoveAsync(
        Guid productId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
