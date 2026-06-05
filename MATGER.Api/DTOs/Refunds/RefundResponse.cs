namespace MATGER.Api.DTOs.Refunds;

public sealed class RefundResponse
{
    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string? Reason { get; init; }

    public string Status { get; init; } = string.Empty;

    public string ProviderReference { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? CompletedAt { get; init; }
}
