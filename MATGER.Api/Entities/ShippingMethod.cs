namespace MATGER.Api.Entities;

public sealed class ShippingMethod
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public decimal BaseCost { get; set; }

    public int EstimatedDeliveryDays { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public List<Order> Orders { get; set; } = [];
}
