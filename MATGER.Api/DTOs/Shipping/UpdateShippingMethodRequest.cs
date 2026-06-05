using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Shipping;

public sealed class UpdateShippingMethodRequest
{
    [MaxLength(120)]
    public string? Name { get; init; }

    [MaxLength(60)]
    public string? Code { get; init; }

    [Range(0, 999999999)]
    public decimal? BaseCost { get; init; }

    [Range(1, 365)]
    public int? EstimatedDeliveryDays { get; init; }

    public bool? IsActive { get; init; }
}
