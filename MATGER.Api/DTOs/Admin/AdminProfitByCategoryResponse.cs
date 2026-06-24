namespace MATGER.Api.DTOs.Admin;

public sealed class AdminProfitByCategoryResponse
{
    public Guid CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public int QuantitySold { get; init; }

    public decimal Revenue { get; init; }

    public decimal Cost { get; init; }

    public decimal GrossProfit { get; init; }

    public decimal GrossMarginPercentage { get; init; }
}
