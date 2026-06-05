using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.ProductVariants;

public sealed class UpdateProductVariantRequest
{
    [MaxLength(150)]
    public string? Name { get; init; }

    [MaxLength(80)]
    public string? SKU { get; init; }

    [Range(0.01, 999999999)]
    public decimal? PriceOverride { get; init; }

    public bool ClearPriceOverride { get; init; }

    public bool? IsActive { get; init; }

    [Range(0, 1000000)]
    public int? QuantityAvailable { get; init; }

    [Range(0, 1000000)]
    public int? LowStockThreshold { get; init; }
}
