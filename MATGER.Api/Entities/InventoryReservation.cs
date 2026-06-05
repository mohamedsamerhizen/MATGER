using MATGER.Api.Enums;

namespace MATGER.Api.Entities;

public sealed class InventoryReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid? ProductVariantId { get; set; }

    public ProductVariant? ProductVariant { get; set; }

    public int Quantity { get; set; }

    public InventoryReservationStatus Status { get; set; } = InventoryReservationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public DateTime? ExpiredAt { get; set; }
}