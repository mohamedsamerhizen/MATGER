namespace MATGER.Api.DTOs.Checkout;

public sealed class CheckoutStartResponse
{
    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public decimal Subtotal { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal ShippingFee { get; init; }

    public decimal Total { get; init; }

    public Guid? CouponId { get; init; }

    public string? CouponCode { get; init; }

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

    public Guid? ShippingMethodId { get; init; }

    public string? ShippingMethodName { get; init; }

    public string? ShippingMethodCode { get; init; }

    public int? ShippingEstimatedDeliveryDays { get; init; }

    public string ShippingStatus { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime PaymentReservationExpiresAt { get; init; }

    public IReadOnlyList<CheckoutOrderItemResponse> Items { get; init; } = [];
}
