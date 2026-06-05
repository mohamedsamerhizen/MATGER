using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Checkout;

public sealed class FailPaymentRequest
{
    [Required]
    public Guid OrderId { get; init; }

    [MaxLength(500)]
    public string? FailureReason { get; init; }
}