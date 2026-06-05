using MATGER.Api.Enums;

namespace MATGER.Api.Entities;

public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid? InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public Guid? ProductVariantId { get; set; }

    public ProductVariant? ProductVariant { get; set; }

    public InventoryMovementType Type { get; set; }

    public int QuantityChange { get; set; }

    public int QuantityAvailableBefore { get; set; }

    public int QuantityAvailableAfter { get; set; }

    public int QuantityReservedBefore { get; set; }

    public int QuantityReservedAfter { get; set; }

    public string? Reason { get; set; }

    public string? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public Guid? ActorUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
