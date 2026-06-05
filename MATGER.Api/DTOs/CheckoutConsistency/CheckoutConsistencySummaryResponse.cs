namespace MATGER.Api.DTOs.CheckoutConsistency;

public sealed class CheckoutConsistencySummaryResponse
{
    public DateTime GeneratedAt { get; init; }

    public string HealthStatus { get; init; } = string.Empty;

    public int OpenIssuesCount { get; init; }

    public int CriticalIssuesCount { get; init; }

    public int WarningIssuesCount { get; init; }

    public int PendingPaymentOrdersCount { get; init; }

    public int ExpiredPendingReservationsCount { get; init; }

    public int PendingPaymentOrdersWithExpiredReservationsCount { get; init; }

    public int PendingPaymentOrdersWithoutReservationsCount { get; init; }

    public int PaymentFailedOrdersWithPendingReservationsCount { get; init; }

    public int PaidOrdersWithPendingReservationsCount { get; init; }

    public int PaidOrdersWithoutSucceededPaymentCount { get; init; }

    public int SucceededPaymentsWithInvalidOrderStatusCount { get; init; }

    public int ProductsWithReservedQuantityMismatchCount { get; init; }

    public int ProductsWithNegativeInventoryCount { get; init; }
}
