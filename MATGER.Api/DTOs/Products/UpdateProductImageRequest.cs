using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class UpdateProductImageRequest
{
    [Url]
    [MaxLength(2048)]
    public string? ImageUrl { get; init; }

    [MaxLength(200)]
    public string? AltText { get; init; }

    public bool? IsPrimary { get; init; }

    [Range(0, 10000)]
    public int? SortOrder { get; init; }
}
