namespace MATGER.Api.DTOs.ProductVariants;

public sealed class ProductVariantResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string SKU { get; init; } = string.Empty;

    public decimal ProductPrice { get; init; }

    public decimal? PriceOverride { get; init; }

    public decimal EffectivePrice { get; init; }

    public bool IsActive { get; init; }

    public int QuantityAvailable { get; init; }

    public int QuantityReserved { get; init; }

    public int LowStockThreshold { get; init; }

    public bool IsInStock { get; init; }

    public bool IsLowStock { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
