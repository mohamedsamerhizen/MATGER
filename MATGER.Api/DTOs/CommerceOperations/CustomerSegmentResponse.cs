namespace MATGER.Api.DTOs.CommerceOperations;

public sealed class CustomerSegmentResponse
{
    public Guid UserId { get; init; }

    public string CustomerEmail { get; init; } = string.Empty;

    public string CustomerFullName { get; init; } = string.Empty;

    public int OrdersCount { get; init; }

    public int DeliveredOrdersCount { get; init; }

    public int RefundsCount { get; init; }

    public decimal TotalSpent { get; init; }

    public DateTime? LastOrderAt { get; init; }

    public string Segment { get; init; } = string.Empty;
}
