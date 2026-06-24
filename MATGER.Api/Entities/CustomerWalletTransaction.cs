using MATGER.Api.Enums;
using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class CustomerWalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WalletId { get; set; }

    public CustomerWallet Wallet { get; set; } = null!;

    public decimal Amount { get; set; }

    public CustomerWalletTransactionType Type { get; set; }

    public string ReferenceType { get; set; } = string.Empty;

    public string? ReferenceId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
