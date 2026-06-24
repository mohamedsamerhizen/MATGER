namespace MATGER.Api.DTOs.Wallet;

public sealed class CustomerWalletTransactionResponse
{
    public Guid Id { get; init; }

    public Guid WalletId { get; init; }

    public decimal Amount { get; init; }

    public string Type { get; init; } = string.Empty;

    public string ReferenceType { get; init; } = string.Empty;

    public string? ReferenceId { get; init; }

    public string? Note { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public Guid? CreatedByUserId { get; init; }
}
