namespace MATGER.Api.DTOs.Orders;

public sealed class OrderResponse
{
    public Guid Id { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public Guid UserId { get; init; }

    public string Status { get; init; } = string.Empty;

    public decimal Subtotal { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal ShippingFee { get; init; }

    public decimal Total { get; init; }

    public Guid? CouponId { get; init; }

    public string? CouponCode { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public DateTime? ShippedAt { get; init; }

    public DateTime? DeliveredAt { get; init; }

    public DateTime? CancelledAt { get; init; }

    public string? CancellationReason { get; init; }

    public Guid? ShippingMethodId { get; init; }

    public string? ShippingMethodName { get; init; }

    public string? ShippingMethodCode { get; init; }

    public int? ShippingEstimatedDeliveryDays { get; init; }

    public string ShippingStatus { get; init; } = string.Empty;

    public string? ShippingCarrier { get; init; }

    public string? TrackingNumber { get; init; }

    public string? DeliveryNote { get; init; }

    public Guid? ShippingAddressId { get; init; }

    public string? ShippingFullName { get; init; }

    public string? ShippingPhoneNumber { get; init; }

    public string? ShippingCountry { get; init; }

    public string? ShippingCity { get; init; }

    public string? ShippingArea { get; init; }

    public string? ShippingStreet { get; init; }

    public string? ShippingBuilding { get; init; }

    public string? ShippingFloor { get; init; }

    public string? ShippingApartment { get; init; }

    public string? ShippingPostalCode { get; init; }

    public string? ShippingNotes { get; init; }

    public IReadOnlyList<OrderItemResponse> Items { get; init; } = [];

    public IReadOnlyList<OrderReservationResponse> Reservations { get; init; } = [];
}
