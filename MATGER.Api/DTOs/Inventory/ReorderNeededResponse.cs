namespace MATGER.Api.DTOs.Inventory;

public sealed class ReorderNeededResponse
{
    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string SKU { get; init; } = string.Empty;

    public Guid? VariantId { get; init; }

    public string? VariantName { get; init; }

    public string? SupplierName { get; init; }

    public string? SupplierSku { get; init; }

    public int AvailableQuantity { get; init; }

    public int ReservedQuantity { get; init; }

    public int ReorderPoint { get; init; }

    public int SuggestedReorderQuantity { get; init; }

    public int? LeadTimeDays { get; init; }

    public string? BinLocation { get; init; }

    public string Severity { get; init; } = string.Empty;
}
