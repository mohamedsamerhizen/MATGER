using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Returns;
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
[Authorize]
public sealed class ReturnsController(
    ApplicationDbContext dbContext,
    IReturnService returnService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [HttpPost("orders/{orderId:guid}/returns")]
    public async Task<ActionResult<ReturnRequestResponse>> Create(
        Guid orderId,
        CreateReturnRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await returnService.CreateAsync(
            orderId,
            request,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [HttpGet("orders/{orderId:guid}/returns")]
    public async Task<ActionResult<IReadOnlyList<ReturnRequestResponse>>> GetByOrder(
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

        var order = await dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (order is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        if (!CanSeeAllOrders() && order.UserId != userId.Value)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        var returns = await dbContext.ReturnRequests
            .AsNoTracking()
            .Include(returnRequest => returnRequest.Order)
            .Where(returnRequest => returnRequest.OrderId == orderId)
            .OrderByDescending(returnRequest => returnRequest.RequestedAt)
            .Select(returnRequest => ToResponse(returnRequest))
            .ToListAsync(cancellationToken);

        return Ok(returns);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("returns")]
    public async Task<ActionResult<PaginatedResponse<ReturnRequestResponse>>> GetAll(
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.ReturnRequests
            .AsNoTracking()
            .Include(returnRequest => returnRequest.Order)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ReturnRequestStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Return request status is invalid."));
            }

            query = query.Where(returnRequest => returnRequest.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var returns = await query
            .OrderByDescending(returnRequest => returnRequest.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(returnRequest => ToResponse(returnRequest))
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<ReturnRequestResponse>.Create(
            returns,
            page,
            pageSize,
            totalCount));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost("returns/{id:guid}/approve")]
    public async Task<ActionResult<ReturnRequestResponse>> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await returnService.ApproveAsync(
            id,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost("returns/{id:guid}/reject")]
    public async Task<ActionResult<ReturnRequestResponse>> Reject(
        Guid id,
        RejectReturnRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await returnService.RejectAsync(
            id,
            request,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost("returns/{id:guid}/complete")]
    public async Task<ActionResult<ReturnRequestResponse>> Complete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await returnService.CompleteAsync(
            id,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    private bool CanSeeAllOrders()
    {
        return currentUserService.IsInRole(ApplicationRoles.Admin) ||
               currentUserService.IsInRole(ApplicationRoles.OrderManager);
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

    private static ReturnRequestResponse ToResponse(ReturnRequest returnRequest)
    {
        return new ReturnRequestResponse
        {
            Id = returnRequest.Id,
            OrderId = returnRequest.OrderId,
            OrderNumber = returnRequest.Order.OrderNumber,
            UserId = returnRequest.UserId,
            Reason = returnRequest.Reason,
            Status = returnRequest.Status.ToString(),
            CurrentOrderStatus = returnRequest.Order.Status.ToString(),
            AdminNote = returnRequest.AdminNote,
            RequestedAt = returnRequest.RequestedAt,
            ApprovedAt = returnRequest.ApprovedAt,
            RejectedAt = returnRequest.RejectedAt,
            CompletedAt = returnRequest.CompletedAt
        };
    }
}
