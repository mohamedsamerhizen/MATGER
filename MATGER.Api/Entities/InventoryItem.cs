namespace MATGER.Api.Entities;

public sealed class InventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int QuantityAvailable { get; set; }

    public int QuantityReserved { get; set; }

    public int LowStockThreshold { get; set; } = 5;

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}