namespace MATGER.Api.DTOs.Customers;

public sealed class CustomerProfileResponse
{
    public Guid UserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public bool IsActive { get; init; }

    public int OrdersCount { get; init; }

    public decimal TotalSpent { get; init; }

    public decimal AverageOrderValue { get; init; }

    public DateTime? LastOrderDate { get; init; }

    public int RefundsCount { get; init; }

    public decimal ReturnRatio { get; init; }

    public int ReviewsCount { get; init; }

    public int WishlistCount { get; init; }

    public CustomerActiveCartSummaryResponse ActiveCart { get; init; } = new();

    public string CustomerSegment { get; init; } = string.Empty;

    public string RiskLevel { get; init; } = string.Empty;
}
