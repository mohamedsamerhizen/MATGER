namespace MATGER.Api.DTOs.Admin;

public sealed class AdminSalesReportResponse
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    public DateTime GeneratedAt { get; init; }

    public int TotalOrdersCreated { get; init; }

    public int PendingPaymentOrders { get; init; }

    public int PaymentFailedOrders { get; init; }

    public int PaidOrders { get; init; }

    public int ProcessingOrders { get; init; }

    public int ShippedOrders { get; init; }

    public int DeliveredOrders { get; init; }

    public int CancelledOrders { get; init; }

    public int ReturnRequestedOrders { get; init; }

    public int ReturnedOrders { get; init; }

    public int RefundedOrders { get; init; }

    public int RevenueOrdersCount { get; init; }

    public int UniqueCustomersCount { get; init; }

    public int ItemsSold { get; init; }

    public decimal GrossRevenue { get; init; }

    public decimal TotalDiscountAmount { get; init; }

    public decimal TotalShippingFees { get; init; }

    public decimal RefundedAmount { get; init; }

    public decimal NetRevenue { get; init; }

    public decimal AverageOrderValue { get; init; }
}
