using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Orders;

public sealed class AdminCancelOrderRequest
{
    [MaxLength(500)]
    public string? Reason { get; init; }

    [MaxLength(2000)]
    public string? InternalNote { get; init; }
}
