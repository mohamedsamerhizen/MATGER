using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.ProductVariants;

public sealed class CreateProductVariantRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string SKU { get; init; } = string.Empty;

    [Range(0.01, 999999999)]
    public decimal? PriceOverride { get; init; }

    public bool IsActive { get; init; } = true;

    [Range(0, 1000000)]
    public int QuantityAvailable { get; init; }

    [Range(0, 1000000)]
    public int LowStockThreshold { get; init; } = 5;
}
