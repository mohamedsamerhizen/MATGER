using System.ComponentModel.DataAnnotations;
using MATGER.Api.Enums;

namespace MATGER.Api.DTOs.Shipping;

public sealed class UpdateOrderShippingRequest
{
    public Guid? ShippingMethodId { get; init; }

    public ShippingStatus? ShippingStatus { get; init; }

    [MaxLength(120)]
    public string? CarrierName { get; init; }

    [MaxLength(120)]
    public string? TrackingNumber { get; init; }

    [MaxLength(500)]
    public string? ShippingNote { get; init; }
}
