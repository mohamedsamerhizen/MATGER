namespace MATGER.Api.DTOs.Inventory;

public sealed class InventoryMovementResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public Guid? InventoryItemId { get; init; }

    public Guid? ProductVariantId { get; init; }

    public string? ProductVariantName { get; init; }

    public string? ProductVariantSku { get; init; }

    public string Type { get; init; } = string.Empty;

    public int QuantityChange { get; init; }

    public int QuantityAvailableBefore { get; init; }

    public int QuantityAvailableAfter { get; init; }

    public int QuantityReservedBefore { get; init; }

    public int QuantityReservedAfter { get; init; }

    public string? Reason { get; init; }

    public string? ReferenceType { get; init; }

    public string? ReferenceId { get; init; }

    public Guid? ActorUserId { get; init; }

    public DateTime CreatedAt { get; init; }
}
