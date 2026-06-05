namespace MATGER.Api.DTOs.CheckoutConsistency;

public sealed class CheckoutConsistencyIssueResponse
{
    public string IssueType { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;

    public DateTime DetectedAt { get; init; }

    public Guid? OrderId { get; init; }

    public string? OrderNumber { get; init; }

    public string? OrderStatus { get; init; }

    public decimal? OrderTotal { get; init; }

    public Guid? PaymentId { get; init; }

    public string? PaymentStatus { get; init; }

    public Guid? ProductId { get; init; }

    public string? ProductName { get; init; }

    public int? PendingReservationsCount { get; init; }

    public DateTime? OldestReservationExpiresAt { get; init; }

    public int? ActualReservedQuantity { get; init; }

    public int? ExpectedReservedQuantity { get; init; }
}
