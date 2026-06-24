using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class UpdateProductSpecificationRequest
{
    [MaxLength(120)]
    public string? Name { get; init; }

    [MaxLength(500)]
    public string? Value { get; init; }

    [MaxLength(120)]
    public string? GroupName { get; init; }

    [Range(0, 10000)]
    public int? SortOrder { get; init; }
}
