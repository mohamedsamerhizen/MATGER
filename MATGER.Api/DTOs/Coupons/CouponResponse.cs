using MATGER.Api.Enums;

namespace MATGER.Api.DTOs.Coupons;

public sealed class CouponResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public CouponDiscountType DiscountType { get; init; }

    public decimal DiscountValue { get; init; }

    public decimal? MaxDiscountAmount { get; init; }

    public decimal MinimumOrderSubtotal { get; init; }

    public DateTime StartsAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public bool IsActive { get; init; }

    public int? UsageLimit { get; init; }

    public int UsageCount { get; init; }

    public int? PerCustomerUsageLimit { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
