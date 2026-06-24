namespace MATGER.Api.DTOs.Admin;

public sealed class AdminSalesOverviewResponse
{
    public decimal SalesToday { get; init; }

    public decimal SalesLast7Days { get; init; }

    public decimal SalesLast30Days { get; init; }

    public int OrdersToday { get; init; }

    public int OrdersLast7Days { get; init; }

    public int OrdersLast30Days { get; init; }

    public decimal AverageOrderValueLast30Days { get; init; }

    public decimal RefundAmountLast30Days { get; init; }

    public decimal RefundRateLast30Days { get; init; }

    public DateTime GeneratedAtUtc { get; init; }
}
