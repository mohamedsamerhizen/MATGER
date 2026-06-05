namespace MATGER.Api.DTOs.Cart;

public sealed class CartItemResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string SKU { get; init; } = string.Empty;

    public Guid? ProductVariantId { get; init; }

    public string? VariantName { get; init; }

    public string? VariantSku { get; init; }

    public int Quantity { get; init; }

    public decimal UnitPriceSnapshot { get; init; }

    public decimal Total => UnitPriceSnapshot * Quantity;
}