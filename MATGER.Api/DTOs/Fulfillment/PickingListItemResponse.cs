namespace MATGER.Api.DTOs.Fulfillment;

public sealed class PickingListItemResponse
{
    public string SKU { get; init; } = string.Empty;

    public Guid ProductId { get; init; }

    public Guid? VariantId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string? VariantName { get; init; }

    public int TotalQuantityToPick { get; init; }

    public int NumberOfOrders { get; init; }

    public int CurrentAvailableStock { get; init; }

    public int CurrentReservedStock { get; init; }

    public string StockWarning { get; init; } = string.Empty;
}
