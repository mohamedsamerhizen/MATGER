namespace MATGER.Api.DTOs.Products;

public sealed class ProductSpecificationResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string? GroupName { get; init; }

    public int SortOrder { get; init; }
}
