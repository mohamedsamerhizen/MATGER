using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Categories;

public sealed class CreateCategoryRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;
}