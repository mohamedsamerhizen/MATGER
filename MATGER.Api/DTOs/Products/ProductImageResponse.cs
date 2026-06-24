namespace MATGER.Api.DTOs.Products;

public sealed class ProductImageResponse
{
    public Guid Id { get; init; }

    public string ImageUrl { get; init; } = string.Empty;

    public string? AltText { get; init; }

    public bool IsPrimary { get; init; }

    public int SortOrder { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
