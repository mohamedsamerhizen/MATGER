using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Loyalty;

public sealed class AdjustLoyaltyPointsRequest
{
    [Range(-1000000, 1000000)]
    public int Points { get; init; }

    [Required]
    [MaxLength(500)]
    public string Note { get; init; } = string.Empty;
}
