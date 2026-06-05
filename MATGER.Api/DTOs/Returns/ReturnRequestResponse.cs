namespace MATGER.Api.DTOs.Returns;

public sealed class ReturnRequestResponse
{
    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public Guid UserId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string CurrentOrderStatus { get; init; } = string.Empty;

    public string? AdminNote { get; init; }

    public DateTime RequestedAt { get; init; }

    public DateTime? ApprovedAt { get; init; }

    public DateTime? RejectedAt { get; init; }

    public DateTime? CompletedAt { get; init; }
}
