namespace MATGER.Api.DTOs.Admin;

public sealed class AdminRevenueChartPointResponse
{
    public DateTime Date { get; init; }

    public int OrdersCount { get; init; }

    public int ItemsSold { get; init; }

    public decimal GrossRevenue { get; init; }

    public decimal RefundedAmount { get; init; }

    public decimal NetRevenue { get; init; }
}
