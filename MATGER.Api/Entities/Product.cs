namespace MATGER.Api.Entities;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string SKU { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsFeatured { get; set; }

    public decimal? WeightKg { get; set; }

    public decimal? LengthCm { get; set; }

    public decimal? WidthCm { get; set; }

    public decimal? HeightCm { get; set; }

    public bool IsReturnable { get; set; } = true;

    public int ReturnWindowDays { get; set; } = 14;

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public InventoryItem? InventoryItem { get; set; }

    public List<ProductVariant> Variants { get; set; } = [];

    public List<CartItem> CartItems { get; set; } = [];

    public List<WishlistItem> WishlistItems { get; set; } = [];

    public List<ProductReview> Reviews { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
