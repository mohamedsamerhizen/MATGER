using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Orders;

public sealed class ShipOrderRequest
{
    [MaxLength(120)]
    public string? ShippingCarrier { get; init; }

    [MaxLength(120)]
    public string? TrackingNumber { get; init; }
}
