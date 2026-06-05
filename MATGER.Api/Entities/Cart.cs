using MATGER.Api.Enums;
using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class Cart
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public CartStatus Status { get; set; } = CartStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    public Guid? CouponId { get; set; }

    public Coupon? Coupon { get; set; }

    public string? CouponCodeSnapshot { get; set; }

    public decimal DiscountAmount { get; set; }

    public List<CartItem> Items { get; set; } = [];
}
