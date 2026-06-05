namespace MATGER.Api.DTOs.Inventory;

public sealed class InventoryAttentionItemResponse
{
    public Guid InventoryItemId { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public decimal ProductPrice { get; init; }

    public Guid CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public int QuantityAvailable { get; init; }

    public int QuantityReserved { get; init; }

    public int QuantityOnHand { get; init; }

    public int LowStockThreshold { get; init; }

    public bool IsOutOfStock { get; init; }

    public bool IsLowStock { get; init; }

    public bool HasReservedQuantity { get; init; }

    public bool HasNegativeStock { get; init; }

    public int RestockSuggestedQuantity { get; init; }

    public decimal EstimatedAvailableValue { get; init; }

    public decimal EstimatedOnHandValue { get; init; }

    public string Severity { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
