using MATGER.Api.Enums;

namespace MATGER.Api.Entities;

public sealed class Refund
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    public string ProviderReference { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}
