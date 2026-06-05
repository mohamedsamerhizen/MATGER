namespace MATGER.Api.DTOs.Coupons;

public sealed class ValidateCouponResponse
{
    public bool IsValid { get; init; }

    public Guid? CouponId { get; init; }

    public string Code { get; init; } = string.Empty;

    public decimal DiscountAmount { get; init; }

    public string Message { get; init; } = string.Empty;
}
