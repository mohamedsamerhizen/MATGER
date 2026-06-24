using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Wallet;

public sealed class WalletAdjustmentRequest
{
    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal Amount { get; init; }

    [Required]
    [MaxLength(500)]
    public string Note { get; init; } = string.Empty;
}
