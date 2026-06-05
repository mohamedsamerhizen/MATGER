namespace MATGER.Api.DTOs.CheckoutConsistency;

public sealed class CheckoutMaintenanceRunResponse
{
    public DateTime StartedAt { get; init; }

    public DateTime CompletedAt { get; init; }

    public int ExpiredReservationsCount { get; init; }

    public int PaymentFailedOrdersCount { get; init; }

    public IReadOnlyList<Guid> AffectedOrderIds { get; init; } = Array.Empty<Guid>();

    public IReadOnlyList<Guid> ExpiredReservationIds { get; init; } = Array.Empty<Guid>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
