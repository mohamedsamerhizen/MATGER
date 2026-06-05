using System.Text.Json;
using System.Text.Json.Serialization;
using MATGER.Api.Data;
using MATGER.Api.Entities;
using MATGER.Api.Interfaces;

namespace MATGER.Api.Services;

public sealed class AuditLogService(ApplicationDbContext dbContext) : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };

    public async Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityName,
        string entityId,
        object? oldValue = null,
        object? newValue = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Audit action is required.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("Audit entity name is required.", nameof(entityName));
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentException("Audit entity id is required.", nameof(entityId));
        }

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action.Trim(),
            EntityName = entityName.Trim(),
            EntityId = entityId.Trim(),
            OldValueJson = SerializeOrNull(oldValue),
            NewValueJson = SerializeOrNull(newValue),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }

    private static string? SerializeOrNull(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(value, JsonOptions);
    }
}