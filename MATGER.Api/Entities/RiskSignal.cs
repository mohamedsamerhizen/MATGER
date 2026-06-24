using MATGER.Api.Enums;
using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class RiskSignal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? OrderId { get; set; }

    public Order? Order { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public string SignalType { get; set; } = string.Empty;

    public RiskSignalSeverity Severity { get; set; } = RiskSignalSeverity.Low;

    public string Details { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public RiskSignalStatus Status { get; set; } = RiskSignalStatus.Open;

    public Guid? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ResolutionNote { get; set; }
}
