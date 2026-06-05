namespace MATGER.Api.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityName,
        string entityId,
        object? oldValue = null,
        object? newValue = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
}