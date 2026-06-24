namespace MATGER.Api.DTOs.Loyalty;

public sealed class LoyaltySummaryResponse
{
    public int Accounts { get; init; }

    public int PointsOutstanding { get; init; }

    public int LifetimeEarned { get; init; }

    public int LifetimeRedeemed { get; init; }

    public int Transactions { get; init; }
}
