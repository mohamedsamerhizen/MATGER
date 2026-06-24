namespace MATGER.Api.DTOs.Wallet;

public sealed class CustomerWalletResponse
{
    public Guid WalletId { get; init; }

    public Guid UserId { get; init; }

    public decimal Balance { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
