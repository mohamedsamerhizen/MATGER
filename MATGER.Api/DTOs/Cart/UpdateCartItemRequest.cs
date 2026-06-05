using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Cart;

public sealed class UpdateCartItemRequest
{
    [Range(1, 1000000)]
    public int Quantity { get; init; }
}