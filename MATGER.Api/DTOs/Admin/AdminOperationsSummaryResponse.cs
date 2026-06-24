namespace MATGER.Api.DTOs.Admin;

public sealed class AdminOperationsSummaryResponse
{
    public int PendingOrders { get; init; }

    public int PaidAwaitingProcessingOrders { get; init; }

    public int ProcessingOrders { get; init; }

    public int LowStockCount { get; init; }

    public int CriticalStockCount { get; init; }

    public int PendingReturns { get; init; }

    public int PendingRefunds { get; init; }

    public int OpenRiskSignals { get; init; }

    public int PendingStockAdjustmentRequests { get; init; }

    public DateTime GeneratedAtUtc { get; init; }
}
