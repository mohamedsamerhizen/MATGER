namespace MATGER.Api.DTOs.Inventory;

public sealed class InventoryHealthSummaryResponse
{
    public int TotalInventoryItems { get; init; }

    public int ActiveProducts { get; init; }

    public int InStockProducts { get; init; }

    public int LowStockProducts { get; init; }

    public int OutOfStockProducts { get; init; }

    public int ReservedProducts { get; init; }

    public int NegativeStockProducts { get; init; }

    public int TotalQuantityAvailable { get; init; }

    public int TotalQuantityReserved { get; init; }

    public int TotalQuantityOnHand { get; init; }

    public decimal TotalAvailableInventoryValue { get; init; }

    public decimal TotalOnHandInventoryValue { get; init; }

    public DateTime GeneratedAt { get; init; }
}
