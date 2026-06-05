namespace MATGER.Api.Entities;

public sealed class ProductVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string SKU { get; set; } = string.Empty;

    public decimal? PriceOverride { get; set; }

    public bool IsActive { get; set; } = true;

    public int QuantityAvailable { get; set; }

    public int QuantityReserved { get; set; }

    public int LowStockThreshold { get; set; } = 5;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public List<CartItem> CartItems { get; set; } = [];

    public List<OrderItem> OrderItems { get; set; } = [];

    public List<InventoryReservation> InventoryReservations { get; set; } = [];
}
