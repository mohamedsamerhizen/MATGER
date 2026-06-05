using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Orders;

public sealed class DeliverOrderRequest
{
    [MaxLength(500)]
    public string? DeliveryNote { get; init; }
}
