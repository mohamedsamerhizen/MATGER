using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Categories;

public sealed class UpdateCategoryRequest
{
    [MaxLength(100)]
    public string? Name { get; init; }

    public bool? IsActive { get; init; }
}