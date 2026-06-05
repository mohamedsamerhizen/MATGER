using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Inventory;

public sealed class AdjustInventoryRequest
{
    [Range(-1000000, 1000000)]
    public int QuantityChange { get; init; }

    [Range(0, 1000000)]
    public int? LowStockThreshold { get; init; }

    [MaxLength(300)]
    public string? Reason { get; init; }
}