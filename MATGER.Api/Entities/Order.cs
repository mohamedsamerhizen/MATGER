using MATGER.Api.Enums;
using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OrderNumber { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal Total { get; set; }

    public Guid? CouponId { get; set; }

    public Coupon? Coupon { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public Guid? ShippingMethodId { get; set; }

    public ShippingMethod? ShippingMethod { get; set; }

    public string? ShippingMethodNameSnapshot { get; set; }

    public string? ShippingMethodCodeSnapshot { get; set; }

    public int? ShippingEstimatedDeliveryDays { get; set; }

    public ShippingStatus ShippingStatus { get; set; } = ShippingStatus.Pending;

    public string? ShippingCarrier { get; set; }

    public string? TrackingNumber { get; set; }

    public string? DeliveryNote { get; set; }

    public Guid? ShippingAddressId { get; set; }

    public string? ShippingFullName { get; set; }

    public string? ShippingPhoneNumber { get; set; }

    public string? ShippingCountry { get; set; }

    public string? ShippingCity { get; set; }

    public string? ShippingArea { get; set; }

    public string? ShippingStreet { get; set; }

    public string? ShippingBuilding { get; set; }

    public string? ShippingFloor { get; set; }

    public string? ShippingApartment { get; set; }

    public string? ShippingPostalCode { get; set; }

    public string? ShippingNotes { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    public List<InventoryReservation> InventoryReservations { get; set; } = [];

    public List<Payment> Payments { get; set; } = [];

    public CouponRedemption? CouponRedemption { get; set; }

    public List<ReturnRequest> ReturnRequests { get; set; } = [];

    public List<Refund> Refunds { get; set; } = [];

    public List<OrderStatusHistory> StatusHistories { get; set; } = [];

    public List<OrderInternalNote> InternalNotes { get; set; } = [];
}
