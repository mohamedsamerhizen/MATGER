namespace MATGER.Api.DTOs.Inventory;

public sealed class StockAdjustmentRequestResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public Guid? VariantId { get; init; }

    public string? VariantName { get; init; }

    public string? VariantSku { get; init; }

    public Guid RequestedByUserId { get; init; }

    public int QuantityChange { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime RequestedAtUtc { get; init; }

    public Guid? ReviewedByUserId { get; init; }

    public DateTime? ReviewedAtUtc { get; init; }

    public string? ReviewNote { get; init; }

    public Guid? AppliedInventoryMovementId { get; init; }
}
