namespace MATGER.Api.DTOs.Orders;

public sealed class OrderStatusHistoryResponse
{
    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public string? PreviousStatus { get; init; }

    public string NewStatus { get; init; } = string.Empty;

    public Guid? ChangedByUserId { get; init; }

    public string? ChangedByFullName { get; init; }

    public string? ChangedByEmail { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }
}
