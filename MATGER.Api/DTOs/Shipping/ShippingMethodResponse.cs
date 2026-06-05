namespace MATGER.Api.DTOs.Shipping;

public sealed class ShippingMethodResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public decimal BaseCost { get; init; }

    public int EstimatedDeliveryDays { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
