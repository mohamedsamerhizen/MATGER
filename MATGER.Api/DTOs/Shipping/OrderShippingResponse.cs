namespace MATGER.Api.DTOs.Shipping;

public sealed class OrderShippingResponse
{
    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public Guid? ShippingMethodId { get; init; }

    public string? ShippingMethodName { get; init; }

    public string? ShippingMethodCode { get; init; }

    public decimal ShippingFee { get; init; }

    public int? EstimatedDeliveryDays { get; init; }

    public string ShippingStatus { get; init; } = string.Empty;

    public string? CarrierName { get; init; }

    public string? TrackingNumber { get; init; }

    public string? ShippingNote { get; init; }

    public DateTime? ShippedAt { get; init; }

    public DateTime? DeliveredAt { get; init; }
}
