using System.Data;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Inventory;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/inventory/stock-adjustments")]
[Authorize(Policy = AuthorizationPolicies.InventoryManagerOnly)]
public sealed class StockAdjustmentRequestsController(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IAuditLogService auditLogService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<StockAdjustmentRequestResponse>> Create(
        CreateStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        if (request.ProductId == Guid.Empty)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product id is required."));
        }

        if (request.VariantId == Guid.Empty)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Variant id is invalid."));
        }

        if (request.QuantityChange == 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Quantity change cannot be zero."));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Adjustment reason is required."));
        }

        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(product => product.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        if (request.VariantId.HasValue)
        {
            var variantExists = await dbContext.ProductVariants
                .AsNoTracking()
                .AnyAsync(variant =>
                    variant.Id == request.VariantId.Value &&
                    variant.ProductId == product.Id,
                    cancellationToken);

            if (!variantExists)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product variant was not found."));
            }
        }

        var adjustment = new StockAdjustmentRequest
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            VariantId = request.VariantId,
            RequestedByUserId = userId.Value,
            QuantityChange = request.QuantityChange,
            Reason = request.Reason.Trim(),
            Status = StockAdjustmentRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow
        };

        dbContext.StockAdjustmentRequests.Add(adjustment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadResponseAsync(adjustment.Id, cancellationToken);

        return CreatedAtAction(
            nameof(GetAll),
            new { id = adjustment.Id },
            response);
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<StockAdjustmentRequestResponse>>> GetPending(
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var requests = await QueryResponses()
            .Where(request => request.Status == StockAdjustmentRequestStatus.Pending)
            .OrderBy(request => request.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(requests.Select(ToResponse).ToList());
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockAdjustmentRequestResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var requests = await QueryResponses()
            .OrderByDescending(request => request.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(requests.Select(ToResponse).ToList());
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<StockAdjustmentRequestResponse>>> GetMy(
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var requests = await QueryResponses()
            .Where(request => request.RequestedByUserId == userId.Value)
            .OrderByDescending(request => request.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(requests.Select(ToResponse).ToList());
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<StockAdjustmentRequestResponse>> Approve(
        Guid id,
        ReviewStockAdjustmentRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var reviewerUserId = currentUserService.UserId;

        if (reviewerUserId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var adjustment = await dbContext.StockAdjustmentRequests
            .Include(item => item.Product)
            .Include(item => item.Variant)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (adjustment is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Stock adjustment request was not found."));
        }

        if (adjustment.Status != StockAdjustmentRequestStatus.Pending)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Only pending stock adjustment requests can be approved."));
        }

        var now = DateTime.UtcNow;
        InventoryMovement movement;

        if (adjustment.VariantId.HasValue)
        {
            var variant = adjustment.Variant;

            if (variant is null)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product variant was not found."));
            }

            var availableBefore = variant.QuantityAvailable;
            var availableAfter = availableBefore + adjustment.QuantityChange;

            if (availableAfter < 0)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Stock adjustment would make variant stock negative."));
            }

            variant.QuantityAvailable = availableAfter;
            variant.UpdatedAt = now;

            movement = CreateMovement(
                productId: adjustment.ProductId,
                inventoryItemId: null,
                productVariantId: variant.Id,
                quantityChange: adjustment.QuantityChange,
                quantityAvailableBefore: availableBefore,
                quantityAvailableAfter: availableAfter,
                quantityReservedBefore: variant.QuantityReserved,
                quantityReservedAfter: variant.QuantityReserved,
                reason: adjustment.Reason,
                referenceId: adjustment.Id.ToString(),
                actorUserId: reviewerUserId.Value,
                createdAt: now);
        }
        else
        {
            var inventoryItem = await dbContext.InventoryItems
                .FirstOrDefaultAsync(item => item.ProductId == adjustment.ProductId, cancellationToken);

            if (inventoryItem is null)
            {
                if (adjustment.QuantityChange < 0)
                {
                    return BadRequest(Error(
                        StatusCodes.Status400BadRequest,
                        "Stock adjustment would make inventory stock negative."));
                }

                inventoryItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = adjustment.ProductId,
                    QuantityAvailable = 0,
                    QuantityReserved = 0,
                    LowStockThreshold = 5,
                    CreatedAt = now
                };

                dbContext.InventoryItems.Add(inventoryItem);
            }

            var availableBefore = inventoryItem.QuantityAvailable;
            var availableAfter = availableBefore + adjustment.QuantityChange;

            if (availableAfter < 0)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Stock adjustment would make inventory stock negative."));
            }

            inventoryItem.QuantityAvailable = availableAfter;
            inventoryItem.UpdatedAt = now;

            movement = CreateMovement(
                productId: adjustment.ProductId,
                inventoryItemId: inventoryItem.Id,
                productVariantId: null,
                quantityChange: adjustment.QuantityChange,
                quantityAvailableBefore: availableBefore,
                quantityAvailableAfter: availableAfter,
                quantityReservedBefore: inventoryItem.QuantityReserved,
                quantityReservedAfter: inventoryItem.QuantityReserved,
                reason: adjustment.Reason,
                referenceId: adjustment.Id.ToString(),
                actorUserId: reviewerUserId.Value,
                createdAt: now);
        }

        dbContext.InventoryMovements.Add(movement);

        adjustment.Status = StockAdjustmentRequestStatus.Approved;
        adjustment.ReviewedByUserId = reviewerUserId.Value;
        adjustment.ReviewedAtUtc = now;
        adjustment.ReviewNote = NormalizeOptional(request?.ReviewNote);
        adjustment.AppliedInventoryMovementId = movement.Id;

        await auditLogService.LogAsync(
            actorUserId: reviewerUserId.Value,
            action: "StockAdjustmentApproved",
            entityName: nameof(StockAdjustmentRequest),
            entityId: adjustment.Id.ToString(),
            oldValue: new
            {
                Status = StockAdjustmentRequestStatus.Pending.ToString()
            },
            newValue: new
            {
                Status = adjustment.Status.ToString(),
                adjustment.ProductId,
                adjustment.VariantId,
                adjustment.QuantityChange,
                adjustment.AppliedInventoryMovementId
            },
            reason: adjustment.ReviewNote ?? "Stock adjustment request approved.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = await LoadResponseAsync(adjustment.Id, cancellationToken);

        return Ok(response);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<StockAdjustmentRequestResponse>> Reject(
        Guid id,
        ReviewStockAdjustmentRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var reviewerUserId = currentUserService.UserId;

        if (reviewerUserId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var adjustment = await dbContext.StockAdjustmentRequests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (adjustment is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Stock adjustment request was not found."));
        }

        if (adjustment.Status != StockAdjustmentRequestStatus.Pending)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Only pending stock adjustment requests can be rejected."));
        }

        adjustment.Status = StockAdjustmentRequestStatus.Rejected;
        adjustment.ReviewedByUserId = reviewerUserId.Value;
        adjustment.ReviewedAtUtc = DateTime.UtcNow;
        adjustment.ReviewNote = NormalizeOptional(request?.ReviewNote);

        await auditLogService.LogAsync(
            actorUserId: reviewerUserId.Value,
            action: "StockAdjustmentRejected",
            entityName: nameof(StockAdjustmentRequest),
            entityId: adjustment.Id.ToString(),
            oldValue: new
            {
                Status = StockAdjustmentRequestStatus.Pending.ToString()
            },
            newValue: new
            {
                Status = adjustment.Status.ToString(),
                adjustment.ProductId,
                adjustment.VariantId,
                adjustment.QuantityChange
            },
            reason: adjustment.ReviewNote ?? "Stock adjustment request rejected.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadResponseAsync(adjustment.Id, cancellationToken);

        return Ok(response);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<StockAdjustmentRequestResponse>> Cancel(
        Guid id,
        ReviewStockAdjustmentRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var adjustment = await dbContext.StockAdjustmentRequests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (adjustment is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Stock adjustment request was not found."));
        }

        if (!IsAdmin() && adjustment.RequestedByUserId != userId.Value)
        {
            return Forbid();
        }

        if (adjustment.Status != StockAdjustmentRequestStatus.Pending)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Only pending stock adjustment requests can be cancelled."));
        }

        adjustment.Status = StockAdjustmentRequestStatus.Cancelled;
        adjustment.ReviewedByUserId = userId.Value;
        adjustment.ReviewedAtUtc = DateTime.UtcNow;
        adjustment.ReviewNote = NormalizeOptional(request?.ReviewNote);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadResponseAsync(adjustment.Id, cancellationToken);

        return Ok(response);
    }

    private IQueryable<StockAdjustmentRequest> QueryResponses()
    {
        return dbContext.StockAdjustmentRequests
            .AsNoTracking()
            .Include(request => request.Product)
            .Include(request => request.Variant);
    }

    private async Task<StockAdjustmentRequestResponse> LoadResponseAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var request = await QueryResponses()
            .FirstAsync(item => item.Id == id, cancellationToken);

        return ToResponse(request);
    }

    private static StockAdjustmentRequestResponse ToResponse(StockAdjustmentRequest request)
    {
        return new StockAdjustmentRequestResponse
        {
            Id = request.Id,
            ProductId = request.ProductId,
            ProductName = request.Product.Name,
            ProductSku = request.Product.SKU,
            VariantId = request.VariantId,
            VariantName = request.Variant?.Name,
            VariantSku = request.Variant?.SKU,
            RequestedByUserId = request.RequestedByUserId,
            QuantityChange = request.QuantityChange,
            Reason = request.Reason,
            Status = request.Status.ToString(),
            RequestedAtUtc = request.RequestedAtUtc,
            ReviewedByUserId = request.ReviewedByUserId,
            ReviewedAtUtc = request.ReviewedAtUtc,
            ReviewNote = request.ReviewNote,
            AppliedInventoryMovementId = request.AppliedInventoryMovementId
        };
    }

    private static InventoryMovement CreateMovement(
        Guid productId,
        Guid? inventoryItemId,
        Guid? productVariantId,
        int quantityChange,
        int quantityAvailableBefore,
        int quantityAvailableAfter,
        int quantityReservedBefore,
        int quantityReservedAfter,
        string reason,
        string referenceId,
        Guid actorUserId,
        DateTime createdAt)
    {
        return new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            InventoryItemId = inventoryItemId,
            ProductVariantId = productVariantId,
            Type = InventoryMovementType.ManualAdjustment,
            QuantityChange = quantityChange,
            QuantityAvailableBefore = quantityAvailableBefore,
            QuantityAvailableAfter = quantityAvailableAfter,
            QuantityReservedBefore = quantityReservedBefore,
            QuantityReservedAfter = quantityReservedAfter,
            Reason = reason,
            ReferenceType = nameof(StockAdjustmentRequest),
            ReferenceId = referenceId,
            ActorUserId = actorUserId,
            CreatedAt = createdAt
        };
    }

    private bool IsAdmin()
    {
        return currentUserService.IsInRole(ApplicationRoles.Admin);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
