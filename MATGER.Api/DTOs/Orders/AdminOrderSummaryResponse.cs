namespace MATGER.Api.DTOs.Orders;

public sealed class AdminOrderSummaryResponse
{
    public OrderResponse Order { get; init; } = new();

    public int PaymentsCount { get; init; }

    public decimal PaymentsTotalAmount { get; init; }

    public int RefundsCount { get; init; }

    public decimal RefundedAmount { get; init; }

    public int ReturnRequestsCount { get; init; }

    public IReadOnlyList<OrderStatusHistoryResponse> StatusHistory { get; init; } = [];

    public IReadOnlyList<OrderInternalNoteResponse> InternalNotes { get; init; } = [];
}
