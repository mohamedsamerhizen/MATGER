namespace MATGER.Api.DTOs.Checkout;

public sealed class PaymentResultResponse
{
    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string OrderStatus { get; init; } = string.Empty;

    public Guid PaymentId { get; init; }

    public string PaymentStatus { get; init; } = string.Empty;

    public string ProviderReference { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? CouponId { get; init; }

    public string? CouponCode { get; init; }

    public decimal DiscountAmount { get; init; }

    public int AttemptNumber { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? ConfirmedAt { get; init; }

    public DateTime? FailedAt { get; init; }
}
