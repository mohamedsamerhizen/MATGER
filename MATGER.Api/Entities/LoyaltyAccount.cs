using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class LoyaltyAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public int PointsBalance { get; set; }

    public int LifetimeEarned { get; set; }

    public int LifetimeRedeemed { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<LoyaltyTransaction> Transactions { get; set; } = [];
}
