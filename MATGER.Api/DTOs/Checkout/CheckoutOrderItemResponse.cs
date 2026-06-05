namespace MATGER.Api.DTOs.Checkout;

public sealed class CheckoutOrderItemResponse
{
    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public Guid? ProductVariantId { get; init; }

    public string? VariantName { get; init; }

    public string? VariantSku { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal Total { get; init; }
}