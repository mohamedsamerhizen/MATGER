using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Inventory;

public sealed class ReviewStockAdjustmentRequest
{
    [MaxLength(500)]
    public string? ReviewNote { get; init; }
}
