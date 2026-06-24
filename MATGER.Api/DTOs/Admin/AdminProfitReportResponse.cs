namespace MATGER.Api.DTOs.Admin;

public sealed class AdminProfitReportResponse
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public DateTime GeneratedAt { get; init; }

    public decimal Revenue { get; init; }

    public decimal Cost { get; init; }

    public decimal GrossProfit { get; init; }

    public decimal GrossMarginPercentage { get; init; }

    public IReadOnlyList<AdminProfitByProductResponse> ProfitByProduct { get; init; } = [];

    public IReadOnlyList<AdminProfitByCategoryResponse> ProfitByCategory { get; init; } = [];

    public IReadOnlyList<AdminLowMarginProductResponse> LowMarginProducts { get; init; } = [];

    public IReadOnlyList<AdminLowMarginProductResponse> NegativeMarginProducts { get; init; } = [];
}
