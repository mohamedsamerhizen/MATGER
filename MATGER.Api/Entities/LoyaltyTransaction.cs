using MATGER.Api.Enums;

namespace MATGER.Api.Entities;

public sealed class LoyaltyTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccountId { get; set; }

    public LoyaltyAccount Account { get; set; } = null!;

    public int Points { get; set; }

    public LoyaltyTransactionType Type { get; set; }

    public string ReferenceType { get; set; } = string.Empty;

    public string? ReferenceId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
