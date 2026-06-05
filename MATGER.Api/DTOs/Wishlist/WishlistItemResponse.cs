namespace MATGER.Api.DTOs.Wishlist;

public sealed class WishlistItemResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public decimal ProductPrice { get; init; }

    public Guid CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}
