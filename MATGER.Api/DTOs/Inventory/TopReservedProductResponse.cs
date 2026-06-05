namespace MATGER.Api.DTOs.Inventory;

public sealed class TopReservedProductResponse
{
    public Guid InventoryItemId { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public int QuantityAvailable { get; init; }

    public int QuantityReserved { get; init; }

    public int QuantityOnHand { get; init; }

    public decimal ReservedSharePercentage { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
