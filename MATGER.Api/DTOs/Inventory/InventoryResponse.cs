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

    public string? SupplierName { get; init; }

    public string? SupplierSku { get; init; }

    public int? ReorderPoint { get; init; }

    public int? ReorderQuantity { get; init; }

    public int? LeadTimeDays { get; init; }

    public string? BinLocation { get; init; }

    public string RowVersion { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
