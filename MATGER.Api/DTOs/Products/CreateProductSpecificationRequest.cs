using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class CreateProductSpecificationRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Value { get; init; } = string.Empty;

    [MaxLength(120)]
    public string? GroupName { get; init; }

    [Range(0, 10000)]
    public int SortOrder { get; init; }
}
