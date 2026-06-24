using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class CreateProductImageRequest
{
    [Required]
    [Url]
    [MaxLength(2048)]
    public string ImageUrl { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? AltText { get; init; }

    public bool IsPrimary { get; init; }

    [Range(0, 10000)]
    public int SortOrder { get; init; }
}
