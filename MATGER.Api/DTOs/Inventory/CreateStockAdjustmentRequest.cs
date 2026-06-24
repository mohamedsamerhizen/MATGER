using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Inventory;

public sealed class CreateStockAdjustmentRequest
{
    [Required]
    public Guid ProductId { get; init; }

    public Guid? VariantId { get; init; }

    [Range(-1000000, 1000000)]
    public int QuantityChange { get; init; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
