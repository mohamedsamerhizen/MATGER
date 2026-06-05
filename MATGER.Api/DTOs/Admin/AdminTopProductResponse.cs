namespace MATGER.Api.DTOs.Admin;

public sealed class AdminTopProductResponse
{
    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductNameSnapshot { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public int OrdersCount { get; init; }

    public int QuantitySold { get; init; }

    public decimal GrossRevenue { get; init; }

    public decimal AverageUnitPrice { get; init; }
}
