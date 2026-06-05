namespace MATGER.Api.DTOs.CommerceOperations;

public sealed class StockReconciliationIssueResponse
{
    public Guid? ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public Guid? ProductVariantId { get; init; }

    public string? VariantName { get; init; }

    public string SKU { get; init; } = string.Empty;

    public int ActualReservedQuantity { get; init; }

    public int ExpectedReservedQuantity { get; init; }

    public int Difference => ActualReservedQuantity - ExpectedReservedQuantity;

    public string Scope { get; init; } = string.Empty;
}
