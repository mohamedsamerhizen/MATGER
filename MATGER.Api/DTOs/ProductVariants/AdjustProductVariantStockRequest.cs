using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.ProductVariants;

public sealed class AdjustProductVariantStockRequest
{
    [Range(-1000000, 1000000)]
    public int QuantityChange { get; init; }

    [MaxLength(500)]
    public string? Reason { get; init; }
}
