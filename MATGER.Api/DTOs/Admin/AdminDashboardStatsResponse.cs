namespace MATGER.Api.DTOs.Admin;

public sealed class AdminDashboardStatsResponse
{
    public int TotalOrders { get; init; }

    public int PendingPaymentOrders { get; init; }

    public int PaidOrders { get; init; }

    public int ProcessingOrders { get; init; }

    public int ShippedOrders { get; init; }

    public int DeliveredOrders { get; init; }

    public int CancelledOrders { get; init; }

    public int PaymentFailedOrders { get; init; }

    public int ReturnRequestedOrders { get; init; }

    public int ReturnedOrders { get; init; }

    public int RefundedOrders { get; init; }

    public decimal TotalRevenue { get; init; }

    public decimal TodayRevenue { get; init; }

    public decimal TotalRefundedAmount { get; init; }

    public int PendingReturnRequests { get; init; }

    public int LowStockProducts { get; init; }

    public int ActiveCustomers { get; init; }

    public int ActiveCoupons { get; init; }
}
