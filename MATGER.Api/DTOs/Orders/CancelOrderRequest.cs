using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Orders;

public sealed class CancelOrderRequest
{
    [MaxLength(500)]
    public string? Reason { get; init; }
}
