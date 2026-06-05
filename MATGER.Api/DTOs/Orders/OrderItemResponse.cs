namespace MATGER.Api.DTOs.Orders;

public sealed class OrderItemResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductNameSnapshot { get; init; } = string.Empty;

    public string ProductSkuSnapshot { get; init; } = string.Empty;

    public Guid? ProductVariantId { get; init; }

    public string? VariantNameSnapshot { get; init; }

    public string? VariantSkuSnapshot { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal Total { get; init; }
}