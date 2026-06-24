namespace MATGER.Api.Entities;

public sealed class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid? ProductVariantId { get; set; }

    public ProductVariant? ProductVariant { get; set; }

    public string ProductNameSnapshot { get; set; } = string.Empty;

    public string ProductSkuSnapshot { get; set; } = string.Empty;

    public string? VariantNameSnapshot { get; set; }

    public string? VariantSkuSnapshot { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? CostPriceSnapshot { get; set; }

    public int Quantity { get; set; }

    public decimal Total { get; set; }
}
