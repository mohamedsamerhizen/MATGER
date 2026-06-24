namespace MATGER.Api.DTOs.Customers;

public sealed class CustomerActiveCartSummaryResponse
{
    public Guid? CartId { get; init; }

    public int ItemsCount { get; init; }

    public int TotalQuantity { get; init; }

    public decimal Subtotal { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal Total { get; init; }
}
