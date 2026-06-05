namespace MATGER.Api.DTOs.Admin;

public sealed class AdminCustomerInsightResponse
{
    public Guid CustomerId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public int OrdersCount { get; init; }

    public int ItemsPurchased { get; init; }

    public decimal TotalSpent { get; init; }

    public decimal AverageOrderValue { get; init; }

    public DateTime FirstPaidOrderAt { get; init; }

    public DateTime LastPaidOrderAt { get; init; }
}
