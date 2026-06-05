using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Shipping;

public sealed class CreateShippingMethodRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(60)]
    public string Code { get; init; } = string.Empty;

    [Range(0, 999999999)]
    public decimal BaseCost { get; init; }

    [Range(1, 365)]
    public int EstimatedDeliveryDays { get; init; }

    public bool IsActive { get; init; } = true;
}
