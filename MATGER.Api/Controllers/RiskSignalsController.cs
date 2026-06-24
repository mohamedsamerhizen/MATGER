using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Risk;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/risk-signals")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class RiskSignalsController(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("open")]
    public async Task<ActionResult<IReadOnlyList<RiskSignalResponse>>> GetOpen(
        CancellationToken cancellationToken)
    {
        var signals = await QuerySignals()
            .Where(signal => signal.Status == RiskSignalStatus.Open)
            .OrderByDescending(signal => signal.Severity)
            .ThenByDescending(signal => signal.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(signals.Select(ToResponse).ToList());
    }

    [HttpGet("orders/{orderId:guid}")]
    public async Task<ActionResult<IReadOnlyList<RiskSignalResponse>>> GetByOrder(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var signals = await QuerySignals()
            .Where(signal => signal.OrderId == orderId)
            .OrderByDescending(signal => signal.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(signals.Select(ToResponse).ToList());
    }

    [HttpGet("customers/{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<RiskSignalResponse>>> GetByCustomer(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var signals = await QuerySignals()
            .Where(signal => signal.UserId == userId)
            .OrderByDescending(signal => signal.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(signals.Select(ToResponse).ToList());
    }

    [HttpGet("summary")]
    public async Task<ActionResult<RiskSummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        var last24Hours = DateTime.UtcNow.AddHours(-24);

        var grouped = await dbContext.RiskSignals
            .AsNoTracking()
            .GroupBy(signal => new
            {
                signal.Status,
                signal.Severity
            })
            .Select(group => new
            {
                group.Key.Status,
                group.Key.Severity,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var response = new RiskSummaryResponse
        {
            OpenSignals = grouped
                .Where(item => item.Status == RiskSignalStatus.Open)
                .Sum(item => item.Count),
            CriticalOpenSignals = grouped
                .Where(item => item.Status == RiskSignalStatus.Open &&
                    item.Severity == RiskSignalSeverity.Critical)
                .Sum(item => item.Count),
            HighOpenSignals = grouped
                .Where(item => item.Status == RiskSignalStatus.Open &&
                    item.Severity == RiskSignalSeverity.High)
                .Sum(item => item.Count),
            MediumOpenSignals = grouped
                .Where(item => item.Status == RiskSignalStatus.Open &&
                    item.Severity == RiskSignalSeverity.Medium)
                .Sum(item => item.Count),
            LowOpenSignals = grouped
                .Where(item => item.Status == RiskSignalStatus.Open &&
                    item.Severity == RiskSignalSeverity.Low)
                .Sum(item => item.Count),
            ResolvedSignals = grouped
                .Where(item => item.Status == RiskSignalStatus.Resolved)
                .Sum(item => item.Count),
            DismissedSignals = grouped
                .Where(item => item.Status == RiskSignalStatus.Dismissed)
                .Sum(item => item.Count),
            SignalsCreatedLast24Hours = await dbContext.RiskSignals
                .AsNoTracking()
                .CountAsync(signal => signal.CreatedAtUtc >= last24Hours, cancellationToken)
        };

        return Ok(response);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<RiskSignalResponse>> Resolve(
        Guid id,
        ReviewRiskSignalRequest? request,
        CancellationToken cancellationToken)
    {
        return await ReviewAsync(
            id,
            RiskSignalStatus.Resolved,
            request,
            cancellationToken);
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<ActionResult<RiskSignalResponse>> Dismiss(
        Guid id,
        ReviewRiskSignalRequest? request,
        CancellationToken cancellationToken)
    {
        return await ReviewAsync(
            id,
            RiskSignalStatus.Dismissed,
            request,
            cancellationToken);
    }

    private async Task<ActionResult<RiskSignalResponse>> ReviewAsync(
        Guid id,
        RiskSignalStatus status,
        ReviewRiskSignalRequest? request,
        CancellationToken cancellationToken)
    {
        var reviewerUserId = currentUserService.UserId;

        if (reviewerUserId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var signal = await dbContext.RiskSignals
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (signal is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Risk signal was not found."));
        }

        if (signal.Status != RiskSignalStatus.Open)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Only open risk signals can be reviewed."));
        }

        signal.Status = status;
        signal.ReviewedByUserId = reviewerUserId.Value;
        signal.ReviewedAtUtc = DateTime.UtcNow;
        signal.ResolutionNote = NormalizeOptional(request?.ResolutionNote);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await QuerySignals()
            .FirstAsync(item => item.Id == id, cancellationToken);

        return Ok(ToResponse(response));
    }

    private IQueryable<RiskSignal> QuerySignals()
    {
        return dbContext.RiskSignals
            .AsNoTracking()
            .Include(signal => signal.Order)
            .Include(signal => signal.User);
    }

    private static RiskSignalResponse ToResponse(RiskSignal signal)
    {
        return new RiskSignalResponse
        {
            Id = signal.Id,
            OrderId = signal.OrderId,
            OrderNumber = signal.Order?.OrderNumber,
            UserId = signal.UserId,
            CustomerEmail = signal.User.Email ?? string.Empty,
            SignalType = signal.SignalType,
            Severity = signal.Severity.ToString(),
            Details = signal.Details,
            CreatedAtUtc = signal.CreatedAtUtc,
            Status = signal.Status.ToString(),
            ReviewedByUserId = signal.ReviewedByUserId,
            ReviewedAtUtc = signal.ReviewedAtUtc,
            ResolutionNote = signal.ResolutionNote
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
