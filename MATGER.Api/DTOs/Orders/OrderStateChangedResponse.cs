namespace MATGER.Api.DTOs.Orders;

public sealed class OrderStateChangedResponse
{
    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string PreviousStatus { get; init; } = string.Empty;

    public string CurrentStatus { get; init; } = string.Empty;

    public DateTime ChangedAt { get; init; }

    public string? CancellationReason { get; init; }

    public string? ShippingCarrier { get; init; }

    public string? TrackingNumber { get; init; }

    public string? DeliveryNote { get; init; }
}
