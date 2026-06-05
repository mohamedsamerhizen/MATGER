namespace MATGER.Api.DTOs.Cart;

public sealed class CartResponse
{
    public Guid Id { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime ExpiresAt { get; init; }

    public IReadOnlyList<CartItemResponse> Items { get; init; } = [];

    public decimal Subtotal => Items.Sum(item => item.Total);

    public decimal DiscountAmount { get; init; }

    public decimal Total => Math.Max(Subtotal - DiscountAmount, 0m);

    public string? CouponCode { get; init; }
}
