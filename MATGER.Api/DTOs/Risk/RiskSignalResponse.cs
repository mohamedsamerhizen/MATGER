namespace MATGER.Api.DTOs.Risk;

public sealed class RiskSignalResponse
{
    public Guid Id { get; init; }

    public Guid? OrderId { get; init; }

    public string? OrderNumber { get; init; }

    public Guid UserId { get; init; }

    public string CustomerEmail { get; init; } = string.Empty;

    public string SignalType { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public string Status { get; init; } = string.Empty;

    public Guid? ReviewedByUserId { get; init; }

    public DateTime? ReviewedAtUtc { get; init; }

    public string? ResolutionNote { get; init; }
}
