using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Cart;

public sealed class ApplyCouponRequest
{
    [Required]
    [MaxLength(64)]
    public string Code { get; init; } = string.Empty;
}
