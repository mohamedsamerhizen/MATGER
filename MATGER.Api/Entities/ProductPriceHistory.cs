namespace MATGER.Api.Entities;

public sealed class ProductPriceHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public decimal OldPrice { get; set; }

    public decimal NewPrice { get; set; }

    public decimal? OldSalePrice { get; set; }

    public decimal? NewSalePrice { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public string? Reason { get; set; }

    public string ChangeType { get; set; } = string.Empty;
}
