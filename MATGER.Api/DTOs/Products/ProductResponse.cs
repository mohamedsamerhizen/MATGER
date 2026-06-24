namespace MATGER.Api.DTOs.Products;

public sealed class ProductResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string SKU { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public decimal EffectivePrice { get; init; }

    public decimal? SalePrice { get; init; }

    public DateTime? SaleStartAtUtc { get; init; }

    public DateTime? SaleEndAtUtc { get; init; }

    public bool IsSaleActive { get; init; }

    public bool IsActive { get; init; }

    public bool IsFeatured { get; init; }

    public decimal? WeightKg { get; init; }

    public decimal? LengthCm { get; init; }

    public decimal? WidthCm { get; init; }

    public decimal? HeightCm { get; init; }

    public bool IsReturnable { get; init; }

    public int ReturnWindowDays { get; init; }

    public int ActiveVariantsCount { get; init; }

    public Guid CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public string CategorySlug { get; init; } = string.Empty;

    public Guid? BrandId { get; init; }

    public string? BrandName { get; init; }

    public string? BrandSlug { get; init; }

    public int QuantityAvailable { get; init; }

    public int QuantityReserved { get; init; }

    public int LowStockThreshold { get; init; }

    public bool IsInStock { get; init; }

    public bool IsLowStock { get; init; }

    public decimal AverageRating { get; init; }

    public int ReviewsCount { get; init; }

    public IReadOnlyList<ProductImageResponse> Images { get; init; } = [];

    public IReadOnlyList<ProductSpecificationResponse> Specifications { get; init; } = [];

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
