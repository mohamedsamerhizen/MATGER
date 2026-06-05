using MATGER.Api.Data;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;

namespace MATGER.Api.Services;

public sealed class InventoryMovementService(ApplicationDbContext dbContext) : IInventoryMovementService
{
    public async Task LogAsync(
        Guid productId,
        Guid inventoryItemId,
        InventoryMovementType type,
        int quantityChange,
        int quantityAvailableBefore,
        int quantityAvailableAfter,
        int quantityReservedBefore,
        int quantityReservedAfter,
        string? reason = null,
        string? referenceType = null,
        string? referenceId = null,
        Guid? actorUserId = null,
        DateTime? createdAt = null,
        CancellationToken cancellationToken = default,
        Guid? productVariantId = null)
    {
        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            InventoryItemId = inventoryItemId == Guid.Empty ? null : inventoryItemId,
            ProductVariantId = productVariantId,
            Type = type,
            QuantityChange = quantityChange,
            QuantityAvailableBefore = quantityAvailableBefore,
            QuantityAvailableAfter = quantityAvailableAfter,
            QuantityReservedBefore = quantityReservedBefore,
            QuantityReservedAfter = quantityReservedAfter,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ReferenceType = string.IsNullOrWhiteSpace(referenceType) ? null : referenceType.Trim(),
            ReferenceId = string.IsNullOrWhiteSpace(referenceId) ? null : referenceId.Trim(),
            ActorUserId = actorUserId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        await dbContext.InventoryMovements.AddAsync(movement, cancellationToken);
    }
}
