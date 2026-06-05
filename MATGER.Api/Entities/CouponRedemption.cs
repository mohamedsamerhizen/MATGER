using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class CouponRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CouponId { get; set; }

    public Coupon Coupon { get; set; } = null!;

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public string CodeSnapshot { get; set; } = string.Empty;

    public decimal DiscountAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
