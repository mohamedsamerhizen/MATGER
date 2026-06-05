using MATGER.Api.Enums;

namespace MATGER.Api.DTOs.Admin;

public sealed class AdminOrderStatusBreakdownResponse
{
    public OrderStatus Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public int OrdersCount { get; init; }

    public decimal Percentage { get; init; }

    public decimal TotalAmount { get; init; }
}
