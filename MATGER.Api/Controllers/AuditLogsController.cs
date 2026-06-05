using MATGER.Api.Data;
using MATGER.Api.DTOs.AuditLogs;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AuditLogsController(ApplicationDbContext dbContext) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<AuditLogResponse>>> GetAll(
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? action = null,
        [FromQuery] string? entityName = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);

        var query = dbContext.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();

            query = query.Where(auditLog => auditLog.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var normalizedEntityName = entityName.Trim();

            query = query.Where(auditLog => auditLog.EntityName == normalizedEntityName);
        }

        if (actorUserId.HasValue)
        {
            query = query.Where(auditLog => auditLog.ActorUserId == actorUserId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(auditLog => auditLog.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(auditLog => auditLog.CreatedAt <= to.Value);
        }

        var totalCount = await query.CountAsync();

        var auditLogs = await query
            .OrderByDescending(auditLog => auditLog.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(auditLog => new AuditLogResponse
            {
                Id = auditLog.Id,
                ActorUserId = auditLog.ActorUserId,
                Action = auditLog.Action,
                EntityName = auditLog.EntityName,
                EntityId = auditLog.EntityId,
                OldValueJson = auditLog.OldValueJson,
                NewValueJson = auditLog.NewValueJson,
                Reason = auditLog.Reason,
                CreatedAt = auditLog.CreatedAt
            })
            .ToListAsync();

        return Ok(PaginatedResponse<AuditLogResponse>.Create(
            auditLogs,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("entity/{entityName}/{entityId}")]
    public async Task<ActionResult<PaginatedResponse<AuditLogResponse>>> GetByEntity(
        string entityName,
        string entityId,
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Entity name is required."));
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Entity id is required."));
        }

        (page, pageSize) = NormalizePagination(page, pageSize);

        var normalizedEntityName = entityName.Trim();
        var normalizedEntityId = entityId.Trim();

        var query = dbContext.AuditLogs
            .AsNoTracking()
            .Where(auditLog =>
                auditLog.EntityName == normalizedEntityName &&
                auditLog.EntityId == normalizedEntityId);

        var totalCount = await query.CountAsync();

        var auditLogs = await query
            .OrderByDescending(auditLog => auditLog.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(auditLog => new AuditLogResponse
            {
                Id = auditLog.Id,
                ActorUserId = auditLog.ActorUserId,
                Action = auditLog.Action,
                EntityName = auditLog.EntityName,
                EntityId = auditLog.EntityId,
                OldValueJson = auditLog.OldValueJson,
                NewValueJson = auditLog.NewValueJson,
                Reason = auditLog.Reason,
                CreatedAt = auditLog.CreatedAt
            })
            .ToListAsync();

        return Ok(PaginatedResponse<AuditLogResponse>.Create(
            auditLogs,
            page,
            pageSize,
            totalCount));
    }

    private static (int Page, int PageSize) NormalizePagination(int page, int pageSize)
    {
        return (
            Math.Max(page, DefaultPage),
            Math.Clamp(pageSize, 1, MaxPageSize));
    }

    private ApiErrorResponse Error(int statusCode, string message)
    {
        return new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        };
    }
}
