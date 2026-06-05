namespace MATGER.Api.DTOs.CommerceOperations;

public sealed class AbandonedCartResponse
{
    public Guid CartId { get; init; }

    public Guid UserId { get; init; }

    public string CustomerEmail { get; init; } = string.Empty;

    public string CustomerFullName { get; init; } = string.Empty;

    public int ItemsCount { get; init; }

    public decimal CartValue { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime ExpiresAt { get; init; }

    public DateTime LastActivityAt { get; init; }

    public int AgeInDays { get; init; }
}
