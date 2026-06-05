using MATGER.Api.Enums;
using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public OrderStatus? PreviousStatus { get; set; }

    public OrderStatus NewStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public ApplicationUser? ChangedByUser { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
