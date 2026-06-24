namespace MATGER.Api.DTOs.Admin;

public sealed class AdminTopReservedInventoryItemResponse
{
    public Guid InventoryItemId { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public int QuantityAvailable { get; init; }

    public int QuantityReserved { get; init; }

    public int QuantityOnHand { get; init; }

    public decimal ReservedSharePercentage { get; init; }
}
