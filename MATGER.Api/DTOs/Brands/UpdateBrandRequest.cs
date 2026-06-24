using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Brands;

public sealed class UpdateBrandRequest
{
    [MaxLength(120)]
    public string? Name { get; init; }

    public bool? IsActive { get; init; }
}
