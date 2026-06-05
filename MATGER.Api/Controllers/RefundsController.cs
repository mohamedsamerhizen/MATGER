using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Refunds;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class RefundsController(
    ApplicationDbContext dbContext,
    IRefundService refundService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpPost("orders/{orderId:guid}/refund")]
    public async Task<ActionResult<RefundResponse>> Create(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await refundService.CreateAsync(
            orderId,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [HttpGet("orders/{orderId:guid}/refund")]
    public async Task<ActionResult<RefundResponse>> GetByOrder(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var refund = await dbContext.Refunds
            .AsNoTracking()
            .Include(refund => refund.Order)
            .OrderByDescending(refund => refund.CreatedAt)
            .FirstOrDefaultAsync(refund => refund.OrderId == orderId, cancellationToken);

        if (refund is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Refund was not found."));
        }

        return Ok(ToResponse(refund));
    }

    [HttpGet("refunds")]
    public async Task<ActionResult<PaginatedResponse<RefundResponse>>> GetAll(
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.Refunds
            .AsNoTracking()
            .Include(refund => refund.Order)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RefundStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Refund status is invalid."));
            }

            query = query.Where(refund => refund.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var refunds = await query
            .OrderByDescending(refund => refund.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(refund => ToResponse(refund))
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<RefundResponse>.Create(
            refunds,
            page,
            pageSize,
            totalCount));
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

    private static RefundResponse ToResponse(Refund refund)
    {
        return new RefundResponse
        {
            Id = refund.Id,
            OrderId = refund.OrderId,
            OrderNumber = refund.Order.OrderNumber,
            Amount = refund.Amount,
            Reason = refund.Reason,
            Status = refund.Status.ToString(),
            ProviderReference = refund.ProviderReference,
            CreatedAt = refund.CreatedAt,
            CompletedAt = refund.CompletedAt
        };
    }
}
