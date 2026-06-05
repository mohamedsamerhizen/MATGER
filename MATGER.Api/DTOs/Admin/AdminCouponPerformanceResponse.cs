namespace MATGER.Api.DTOs.Admin;

public sealed class AdminCouponPerformanceResponse
{
    public Guid CouponId { get; init; }

    public string CouponCode { get; init; } = string.Empty;

    public string CouponName { get; init; } = string.Empty;

    public int RedemptionsCount { get; init; }

    public int UniqueCustomersCount { get; init; }

    public int OrdersCount { get; init; }

    public decimal TotalDiscountAmount { get; init; }

    public decimal RevenueAfterDiscount { get; init; }

    public decimal AverageDiscountAmount { get; init; }

    public decimal DiscountToRevenuePercentage { get; init; }
}
