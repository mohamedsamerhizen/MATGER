namespace MATGER.Api.DTOs.Admin;

public sealed class AdminLowMarginProductResponse
{
    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public decimal Revenue { get; init; }

    public decimal Cost { get; init; }

    public decimal GrossProfit { get; init; }

    public decimal GrossMarginPercentage { get; init; }
}
