using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Coupons;

public sealed class ValidateCouponRequest
{
    [Required]
    [MaxLength(64)]
    public string Code { get; init; } = string.Empty;

    [Range(0, 999999999)]
    public decimal Subtotal { get; init; }
}
