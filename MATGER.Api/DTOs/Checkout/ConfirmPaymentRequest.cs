using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Checkout;

public sealed class ConfirmPaymentRequest
{
    [Required]
    public Guid OrderId { get; init; }
}