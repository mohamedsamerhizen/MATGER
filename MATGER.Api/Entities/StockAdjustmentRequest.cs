using MATGER.Api.Enums;

namespace MATGER.Api.Entities;

public sealed class StockAdjustmentRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid? VariantId { get; set; }

    public ProductVariant? Variant { get; set; }

    public Guid RequestedByUserId { get; set; }

    public int QuantityChange { get; set; }

    public string Reason { get; set; } = string.Empty;

    public StockAdjustmentRequestStatus Status { get; set; } = StockAdjustmentRequestStatus.Pending;

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewNote { get; set; }

    public Guid? AppliedInventoryMovementId { get; set; }

    public InventoryMovement? AppliedInventoryMovement { get; set; }
}
