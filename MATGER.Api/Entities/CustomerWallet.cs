using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class CustomerWallet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public decimal Balance { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<CustomerWalletTransaction> Transactions { get; set; } = [];
}
