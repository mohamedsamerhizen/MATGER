namespace MATGER.Api.DTOs.Products;

public sealed class ProductPriceHistoryResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public decimal OldPrice { get; init; }

    public decimal NewPrice { get; init; }

    public decimal? OldSalePrice { get; init; }

    public decimal? NewSalePrice { get; init; }

    public Guid? ChangedByUserId { get; init; }

    public DateTime ChangedAtUtc { get; init; }

    public string? Reason { get; init; }

    public string ChangeType { get; init; } = string.Empty;
}
