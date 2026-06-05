using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class CreateProductRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; init; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string SKU { get; init; } = string.Empty;

    [Range(0.01, 999999999)]
    public decimal Price { get; init; }

    public bool IsFeatured { get; init; }

    [Range(0.001, 999999.999)]
    public decimal? WeightKg { get; init; }

    [Range(0.01, 999999.99)]
    public decimal? LengthCm { get; init; }

    [Range(0.01, 999999.99)]
    public decimal? WidthCm { get; init; }

    [Range(0.01, 999999.99)]
    public decimal? HeightCm { get; init; }

    public bool IsReturnable { get; init; } = true;

    [Range(1, 365)]
    public int ReturnWindowDays { get; init; } = 14;

    [Required]
    public Guid CategoryId { get; init; }
}
