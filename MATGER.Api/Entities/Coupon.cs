using MATGER.Api.Enums;

namespace MATGER.Api.Entities;

public sealed class Coupon
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public CouponDiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public decimal MinimumOrderSubtotal { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public int? UsageLimit { get; set; }

    public int UsageCount { get; set; }

    public int? PerCustomerUsageLimit { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public List<CouponRedemption> Redemptions { get; set; } = [];
}
