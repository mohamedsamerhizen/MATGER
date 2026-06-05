using MATGER.Api.Enums;

namespace MATGER.Api.Interfaces;

public interface IInventoryMovementService
{
    Task LogAsync(
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
        Guid? productVariantId = null);
}
