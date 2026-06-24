using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Brands;

public sealed class CreateBrandRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;
}
