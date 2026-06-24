using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class UpdateProductRequest
{
    [MaxLength(150)]
    public string? Name { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [MaxLength(80)]
    public string? SKU { get; init; }

    [Range(0.01, 999999999)]
    public decimal? Price { get; init; }

    [Range(0, 999999999)]
    public decimal? CostPrice { get; init; }

    public Guid? CategoryId { get; init; }

    public Guid? BrandId { get; init; }

    public bool? IsActive { get; init; }

    public bool? IsFeatured { get; init; }

    [Range(0.001, 999999.999)]
    public decimal? WeightKg { get; init; }

    [Range(0.01, 999999.99)]
    public decimal? LengthCm { get; init; }

    [Range(0.01, 999999.99)]
    public decimal? WidthCm { get; init; }

    [Range(0.01, 999999.99)]
    public decimal? HeightCm { get; init; }

    public bool? IsReturnable { get; init; }

    [Range(1, 365)]
    public int? ReturnWindowDays { get; init; }
}
