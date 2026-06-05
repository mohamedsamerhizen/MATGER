namespace MATGER.Api.DTOs.Orders;

public sealed class OrderReservationResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public Guid? ProductVariantId { get; init; }

    public string? ProductVariantName { get; init; }

    public int Quantity { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime ExpiresAt { get; init; }

    public DateTime? ConfirmedAt { get; init; }

    public DateTime? ReleasedAt { get; init; }

    public DateTime? ExpiredAt { get; init; }
}