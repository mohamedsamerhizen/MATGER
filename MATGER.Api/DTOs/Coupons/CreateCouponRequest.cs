using System.ComponentModel.DataAnnotations;
using MATGER.Api.Enums;

namespace MATGER.Api.DTOs.Coupons;

public sealed class CreateCouponRequest
{
    [Required]
    [MaxLength(64)]
    public string Code { get; init; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [EnumDataType(typeof(CouponDiscountType))]
    public CouponDiscountType DiscountType { get; init; }

    [Range(0.01, 999999999)]
    public decimal DiscountValue { get; init; }

    [Range(0.01, 999999999)]
    public decimal? MaxDiscountAmount { get; init; }

    [Range(0, 999999999)]
    public decimal MinimumOrderSubtotal { get; init; }

    public DateTime StartsAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    [Range(1, int.MaxValue)]
    public int? UsageLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int? PerCustomerUsageLimit { get; init; }
}
