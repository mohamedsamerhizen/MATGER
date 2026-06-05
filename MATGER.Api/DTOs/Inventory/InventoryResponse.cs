namespace MATGER.Api.DTOs.Inventory;

public sealed class InventoryResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string SKU { get; init; } = string.Empty;

    public int QuantityAvailable { get; init; }

    public int QuantityReserved { get; init; }

    public int QuantityOnHand => QuantityAvailable + QuantityReserved;

    public int LowStockThreshold { get; init; }

    public bool IsLowStock => QuantityAvailable <= LowStockThreshold;

    public string RowVersion { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}