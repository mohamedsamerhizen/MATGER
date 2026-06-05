using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Cart;

public sealed class AddCartItemRequest
{
    [Required]
    public Guid ProductId { get; init; }

    public Guid? ProductVariantId { get; init; }

    [Range(1, 1000000)]
    public int Quantity { get; init; }
}