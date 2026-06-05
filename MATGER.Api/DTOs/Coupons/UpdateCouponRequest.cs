using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Coupons;

public sealed class UpdateCouponRequest
{
    [MaxLength(160)]
    public string? Name { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Range(0.01, 999999999)]
    public decimal? DiscountValue { get; init; }

    [Range(0.01, 999999999)]
    public decimal? MaxDiscountAmount { get; init; }

    [Range(0, 999999999)]
    public decimal? MinimumOrderSubtotal { get; init; }

    public DateTime? StartsAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    [Range(1, int.MaxValue)]
    public int? UsageLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int? PerCustomerUsageLimit { get; init; }

    public bool? IsActive { get; init; }
}
