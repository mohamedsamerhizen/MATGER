namespace MATGER.Api.DTOs.Categories;

public sealed class CategoryResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}