using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class UpdateProductSaleRequest
{
    [Range(0, 999999999)]
    public decimal SalePrice { get; init; }

    public DateTime SaleStartAtUtc { get; init; }

    public DateTime SaleEndAtUtc { get; init; }

    [MaxLength(500)]
    public string? Reason { get; init; }
}
