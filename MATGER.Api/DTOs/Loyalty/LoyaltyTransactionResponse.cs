namespace MATGER.Api.DTOs.Loyalty;

public sealed class LoyaltyTransactionResponse
{
    public Guid Id { get; init; }

    public Guid AccountId { get; init; }

    public int Points { get; init; }

    public string Type { get; init; } = string.Empty;

    public string ReferenceType { get; init; } = string.Empty;

    public string? ReferenceId { get; init; }

    public string? Note { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
