namespace MATGER.Api.DTOs.Admin;

public sealed class AdminInventoryOverviewResponse
{
    public int TotalInventoryItems { get; init; }

    public int LowStockCount { get; init; }

    public int CriticalStockCount { get; init; }

    public int DeadStockCount { get; init; }

    public int ReservedInventoryItems { get; init; }

    public int TotalQuantityAvailable { get; init; }

    public int TotalQuantityReserved { get; init; }

    public int TotalQuantityOnHand { get; init; }

    public decimal EstimatedCostValue { get; init; }

    public decimal EstimatedRetailValue { get; init; }

    public IReadOnlyList<AdminTopReservedInventoryItemResponse> TopReservedItems { get; init; } = [];

    public DateTime GeneratedAtUtc { get; init; }
}
