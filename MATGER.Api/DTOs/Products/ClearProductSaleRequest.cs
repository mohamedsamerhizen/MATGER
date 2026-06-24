using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class ClearProductSaleRequest
{
    [MaxLength(500)]
    public string? Reason { get; init; }
}
