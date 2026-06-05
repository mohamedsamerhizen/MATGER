using System.Security.Claims;
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
[Route("api/inventory")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class InventoryController(
    ApplicationDbContext dbContext,
    IInventoryMovementService inventoryMovementService,
    IAuditLogService auditLogService) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventoryResponse>>> GetAll()
    {
        var inventory = await dbContext.InventoryItems
            .AsNoTracking()
            .Include(item => item.Product)
            .OrderBy(item => item.Product.Name)
            .Select(item => ToResponse(item))
            .ToListAsync();

        return Ok(inventory);
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyList<InventoryResponse>>> GetLowStock()
    {
        var inventory = await dbContext.InventoryItems
            .AsNoTracking()
            .Include(item => item.Product)
            .Where(item => item.QuantityAvailable <= item.LowStockThreshold)
            .OrderBy(item => item.QuantityAvailable)
            .Select(item => ToResponse(item))
            .ToListAsync();

        return Ok(inventory);
    }

    [HttpGet("{productId:guid}")]
    public async Task<ActionResult<InventoryResponse>> GetByProductId(Guid productId)
    {
        var inventoryItem = await dbContext.InventoryItems
            .AsNoTracking()
            .Include(item => item.Product)
            .FirstOrDefaultAsync(item => item.ProductId == productId);

        if (inventoryItem is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Inventory item was not found."));
        }

        return Ok(ToResponse(inventoryItem));
    }

    [HttpPost("{productId:guid}/adjust")]
    public async Task<ActionResult<InventoryResponse>> Adjust(
        Guid productId,
        AdjustInventoryRequest request)
    {
        var actorUserId = GetCurrentUserId();

        if (actorUserId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        if (request.QuantityChange == 0 && request.LowStockThreshold is null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Quantity change or low stock threshold is required."));
        }

        var product = await dbContext.Products
            .FirstOrDefaultAsync(product => product.Id == productId);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        var inventoryItem = await dbContext.InventoryItems
            .Include(item => item.Product)
            .FirstOrDefaultAsync(item => item.ProductId == productId);

        if (inventoryItem is null)
        {
            inventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Product = product,
                QuantityAvailable = 0,
                QuantityReserved = 0,
                LowStockThreshold = request.LowStockThreshold ?? 5,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.InventoryItems.Add(inventoryItem);
        }

        var quantityAvailableBefore = inventoryItem.QuantityAvailable;
        var quantityReservedBefore = inventoryItem.QuantityReserved;
        var lowStockThresholdBefore = inventoryItem.LowStockThreshold;
        var newAvailableQuantity = inventoryItem.QuantityAvailable + request.QuantityChange;

        if (newAvailableQuantity < 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Inventory quantity cannot be negative."));
        }

        inventoryItem.QuantityAvailable = newAvailableQuantity;

        if (request.LowStockThreshold.HasValue)
        {
            inventoryItem.LowStockThreshold = request.LowStockThreshold.Value;
        }

        inventoryItem.UpdatedAt = DateTime.UtcNow;

        if (request.QuantityChange != 0)
        {
            await inventoryMovementService.LogAsync(
                productId: inventoryItem.ProductId,
                inventoryItemId: inventoryItem.Id,
                type: InventoryMovementType.ManualAdjustment,
                quantityChange: request.QuantityChange,
                quantityAvailableBefore: quantityAvailableBefore,
                quantityAvailableAfter: inventoryItem.QuantityAvailable,
                quantityReservedBefore: quantityReservedBefore,
                quantityReservedAfter: inventoryItem.QuantityReserved,
                reason: request.Reason,
                referenceType: nameof(InventoryItem),
                referenceId: inventoryItem.Id.ToString(),
                actorUserId: actorUserId.Value,
                createdAt: inventoryItem.UpdatedAt);
        }

        await auditLogService.LogAsync(
            actorUserId: actorUserId.Value,
            action: "ManualInventoryAdjusted",
            entityName: nameof(InventoryItem),
            entityId: inventoryItem.Id.ToString(),
            oldValue: new
            {
                QuantityAvailable = quantityAvailableBefore,
                QuantityReserved = quantityReservedBefore,
                LowStockThreshold = lowStockThresholdBefore
            },
            newValue: new
            {
                inventoryItem.QuantityAvailable,
                inventoryItem.QuantityReserved,
                inventoryItem.LowStockThreshold,
                request.QuantityChange,
                request.Reason
            },
            reason: "Inventory was manually adjusted.");

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Inventory was updated by another request. Please retry."));
        }

        return Ok(ToResponse(inventoryItem));
    }

    [HttpGet("{productId:guid}/movements")]
    public async Task<ActionResult<PaginatedResponse<InventoryMovementResponse>>> GetMovements(
        Guid productId,
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? type = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);

        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == productId);

        if (!productExists)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        var query = dbContext.InventoryMovements
            .AsNoTracking()
            .Include(movement => movement.Product)
            .Include(movement => movement.ProductVariant)
            .Where(movement => movement.ProductId == productId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<InventoryMovementType>(type.Trim(), ignoreCase: true, out var parsedType))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Inventory movement type is invalid."));
            }

            query = query.Where(movement => movement.Type == parsedType);
        }

        if (from.HasValue)
        {
            query = query.Where(movement => movement.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(movement => movement.CreatedAt <= to.Value);
        }

        var totalCount = await query.CountAsync();

        var movements = await query
            .OrderByDescending(movement => movement.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(movement => ToMovementResponse(movement))
            .ToListAsync();

        return Ok(PaginatedResponse<InventoryMovementResponse>.Create(
            movements,
            page,
            pageSize,
            totalCount));
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return userId;
    }

    private static (int Page, int PageSize) NormalizePagination(int page, int pageSize)
    {
        return (
            Math.Max(page, DefaultPage),
            Math.Clamp(pageSize, 1, MaxPageSize));
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

    private static InventoryResponse ToResponse(InventoryItem item)
    {
        return new InventoryResponse
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductName = item.Product.Name,
            SKU = item.Product.SKU,
            QuantityAvailable = item.QuantityAvailable,
            QuantityReserved = item.QuantityReserved,
            LowStockThreshold = item.LowStockThreshold,
            RowVersion = Convert.ToBase64String(item.RowVersion),
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static InventoryMovementResponse ToMovementResponse(InventoryMovement movement)
    {
        return new InventoryMovementResponse
        {
            Id = movement.Id,
            ProductId = movement.ProductId,
            ProductName = movement.Product.Name,
            ProductSku = movement.Product.SKU,
            InventoryItemId = movement.InventoryItemId,
            ProductVariantId = movement.ProductVariantId,
            ProductVariantName = movement.ProductVariant?.Name,
            ProductVariantSku = movement.ProductVariant?.SKU,
            Type = movement.Type.ToString(),
            QuantityChange = movement.QuantityChange,
            QuantityAvailableBefore = movement.QuantityAvailableBefore,
            QuantityAvailableAfter = movement.QuantityAvailableAfter,
            QuantityReservedBefore = movement.QuantityReservedBefore,
            QuantityReservedAfter = movement.QuantityReservedAfter,
            Reason = movement.Reason,
            ReferenceType = movement.ReferenceType,
            ReferenceId = movement.ReferenceId,
            ActorUserId = movement.ActorUserId,
            CreatedAt = movement.CreatedAt
        };
    }
}
