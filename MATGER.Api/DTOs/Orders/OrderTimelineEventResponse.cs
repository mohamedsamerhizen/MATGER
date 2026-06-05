namespace MATGER.Api.DTOs.Orders;

public sealed class OrderTimelineEventResponse
{
    public string Event { get; init; } = string.Empty;

    public DateTime OccurredAt { get; init; }

    public string Description { get; init; } = string.Empty;
}