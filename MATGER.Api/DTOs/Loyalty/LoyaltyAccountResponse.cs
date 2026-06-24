namespace MATGER.Api.DTOs.Loyalty;

public sealed class LoyaltyAccountResponse
{
    public Guid AccountId { get; init; }

    public Guid UserId { get; init; }

    public int PointsBalance { get; init; }

    public int LifetimeEarned { get; init; }

    public int LifetimeRedeemed { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
