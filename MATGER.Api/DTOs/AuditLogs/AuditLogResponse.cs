namespace MATGER.Api.DTOs.AuditLogs;

public sealed class AuditLogResponse
{
    public Guid Id { get; set; }

    public Guid? ActorUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? OldValueJson { get; set; }

    public string? NewValueJson { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
}