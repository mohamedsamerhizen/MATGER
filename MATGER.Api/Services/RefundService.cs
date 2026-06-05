using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Refunds;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class RefundService(
    ApplicationDbContext dbContext,
    IAuditLogService auditLogService) : IRefundService
{
    public async Task<ActionResult<RefundResponse>> CreateAsync(
        Guid orderId,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(order => order.Refunds)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (order is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Order was not found.",
                traceId));
        }

        var existingRefund = order.Refunds
            .OrderByDescending(refund => refund.CreatedAt)
            .FirstOrDefault();

        if (existingRefund is not null)
        {
            return new ConflictObjectResult(Error(
                StatusCodes.Status409Conflict,
                "Order already has a refund.",
                traceId));
        }

        if (order.Status != OrderStatus.Returned)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Only returned orders can be refunded.",
                traceId));
        }

        if (!OrderStateMachine.CanTransition(order.Status, OrderStatus.Refunded))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                $"Cannot transition order from {order.Status} to {OrderStatus.Refunded}.",
                traceId));
        }

        var now = DateTime.UtcNow;
        var previousOrderStatus = order.Status;

        var refund = new Refund
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            Amount = order.Total,
            Reason = "Fake refund completed.",
            Status = RefundStatus.Completed,
            ProviderReference = $"FAKE-REFUND-{Guid.NewGuid():N}",
            CreatedAt = now,
            CompletedAt = now
        };

        order.Status = OrderStatus.Refunded;

        dbContext.Refunds.Add(refund);

        await auditLogService.LogAsync(
            actorUserId: actorUserId,
            action: "RefundCompleted",
            entityName: nameof(Refund),
            entityId: refund.Id.ToString(),
            oldValue: new
            {
                OrderStatus = previousOrderStatus.ToString()
            },
            newValue: new
            {
                RefundId = refund.Id,
                OrderId = order.Id,
                order.OrderNumber,
                OrderStatus = order.Status.ToString(),
                refund.Amount,
                RefundStatus = refund.Status.ToString(),
                refund.ProviderReference,
                refund.CompletedAt
            },
            reason: "Fake refund was completed.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(refund, order.OrderNumber));
    }

    private static ApiErrorResponse Error(int statusCode, string message, string traceId)
    {
        return new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = traceId
        };
    }

    private static RefundResponse ToResponse(Refund refund, string orderNumber)
    {
        return new RefundResponse
        {
            Id = refund.Id,
            OrderId = refund.OrderId,
            OrderNumber = orderNumber,
            Amount = refund.Amount,
            Reason = refund.Reason,
            Status = refund.Status.ToString(),
            ProviderReference = refund.ProviderReference,
            CreatedAt = refund.CreatedAt,
            CompletedAt = refund.CompletedAt
        };
    }
}
