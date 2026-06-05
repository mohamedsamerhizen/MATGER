using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Returns;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class ReturnService(
    ApplicationDbContext dbContext,
    IAuditLogService auditLogService,
    IInventoryMovementService inventoryMovementService) : IReturnService
{
    public async Task<ActionResult<ReturnRequestResponse>> CreateAsync(
        Guid orderId,
        CreateReturnRequest request,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Return reason is required.",
                traceId));
        }

        var order = await dbContext.Orders
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(order =>
                order.Id == orderId &&
                order.UserId == userId,
                cancellationToken);

        if (order is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Order was not found.",
                traceId));
        }

        if (order.Status != OrderStatus.Delivered)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Only delivered orders can be returned.",
                traceId));
        }

        if (order.Items.Any(item => !item.Product.IsReturnable))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Order contains non-returnable products.",
                traceId));
        }

        var deliveredAt = order.DeliveredAt ?? DateTime.UtcNow;
        var expiredReturnItem = order.Items
            .FirstOrDefault(item => deliveredAt.AddDays(item.Product.ReturnWindowDays) < DateTime.UtcNow);

        if (expiredReturnItem is not null)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                $"Return window has expired for product '{expiredReturnItem.ProductNameSnapshot}'.",
                traceId));
        }

        if (!OrderStateMachine.CanTransition(order.Status, OrderStatus.ReturnRequested))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                $"Cannot transition order from {order.Status} to {OrderStatus.ReturnRequested}.",
                traceId));
        }

        var hasActiveReturn = await dbContext.ReturnRequests
            .AnyAsync(returnRequest =>
                returnRequest.OrderId == order.Id &&
                (returnRequest.Status == ReturnRequestStatus.Requested ||
                 returnRequest.Status == ReturnRequestStatus.Approved),
                cancellationToken);

        if (hasActiveReturn)
        {
            return new ConflictObjectResult(Error(
                StatusCodes.Status409Conflict,
                "Order already has an active return request.",
                traceId));
        }

        var now = DateTime.UtcNow;
        var previousOrderStatus = order.Status;

        order.Status = OrderStatus.ReturnRequested;

        var returnRequest = new ReturnRequest
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            UserId = userId,
            Reason = request.Reason.Trim(),
            Status = ReturnRequestStatus.Requested,
            RequestedAt = now
        };

        dbContext.ReturnRequests.Add(returnRequest);

        await auditLogService.LogAsync(
            actorUserId: userId,
            action: "ReturnRequested",
            entityName: nameof(ReturnRequest),
            entityId: returnRequest.Id.ToString(),
            oldValue: new
            {
                OrderStatus = previousOrderStatus.ToString()
            },
            newValue: new
            {
                ReturnRequestId = returnRequest.Id,
                OrderId = order.Id,
                order.OrderNumber,
                OrderStatus = order.Status.ToString(),
                ReturnStatus = returnRequest.Status.ToString(),
                returnRequest.Reason,
                returnRequest.RequestedAt
            },
            reason: "Customer requested an order return.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(returnRequest));
    }

    public async Task<ActionResult<ReturnRequestResponse>> ApproveAsync(
        Guid id,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var returnRequest = await LoadReturnRequestAsync(id, cancellationToken);

        if (returnRequest is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Return request was not found.",
                traceId));
        }

        if (returnRequest.Status != ReturnRequestStatus.Requested)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Only requested returns can be approved.",
                traceId));
        }

        var oldValue = ToAuditSnapshot(returnRequest);
        var now = DateTime.UtcNow;

        returnRequest.Status = ReturnRequestStatus.Approved;
        returnRequest.ApprovedAt = now;

        await auditLogService.LogAsync(
            actorUserId: actorUserId,
            action: "ReturnApproved",
            entityName: nameof(ReturnRequest),
            entityId: returnRequest.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(returnRequest),
            reason: "Return request was approved.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(returnRequest));
    }

    public async Task<ActionResult<ReturnRequestResponse>> RejectAsync(
        Guid id,
        RejectReturnRequest? request,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var returnRequest = await LoadReturnRequestAsync(id, cancellationToken);

        if (returnRequest is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Return request was not found.",
                traceId));
        }

        if (returnRequest.Status != ReturnRequestStatus.Requested)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Only requested returns can be rejected.",
                traceId));
        }

        var oldValue = ToAuditSnapshot(returnRequest);
        var previousOrderStatus = returnRequest.Order.Status;
        var now = DateTime.UtcNow;

        returnRequest.Status = ReturnRequestStatus.Rejected;
        returnRequest.AdminNote = NormalizeOptional(request?.AdminNote);
        returnRequest.RejectedAt = now;

        if (returnRequest.Order.Status == OrderStatus.ReturnRequested)
        {
            returnRequest.Order.Status = OrderStatus.Delivered;
        }

        await auditLogService.LogAsync(
            actorUserId: actorUserId,
            action: "ReturnRejected",
            entityName: nameof(ReturnRequest),
            entityId: returnRequest.Id.ToString(),
            oldValue: new
            {
                ReturnRequest = oldValue,
                OrderStatus = previousOrderStatus.ToString()
            },
            newValue: new
            {
                ReturnRequest = ToAuditSnapshot(returnRequest),
                OrderStatus = returnRequest.Order.Status.ToString()
            },
            reason: "Return request was rejected.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(returnRequest));
    }

    public async Task<ActionResult<ReturnRequestResponse>> CompleteAsync(
        Guid id,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var returnRequest = await dbContext.ReturnRequests
            .Include(returnRequest => returnRequest.Order)
            .ThenInclude(order => order.Items)
            .ThenInclude(item => item.ProductVariant)
            .Include(returnRequest => returnRequest.Order)
            .ThenInclude(order => order.Items)
            .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(returnRequest => returnRequest.Id == id, cancellationToken);

        if (returnRequest is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Return request was not found.",
                traceId));
        }

        if (returnRequest.Status != ReturnRequestStatus.Approved)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Only approved returns can be completed.",
                traceId));
        }

        if (returnRequest.Order.Status != OrderStatus.ReturnRequested)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Only return requested orders can be completed.",
                traceId));
        }

        if (!OrderStateMachine.CanTransition(returnRequest.Order.Status, OrderStatus.Returned))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                $"Cannot transition order from {returnRequest.Order.Status} to {OrderStatus.Returned}.",
                traceId));
        }

        var oldValue = ToAuditSnapshot(returnRequest);
        var previousOrderStatus = returnRequest.Order.Status;
        var now = DateTime.UtcNow;
        var restockedItems = new List<object>();

        foreach (var orderItem in returnRequest.Order.Items)
        {
            if (orderItem.ProductVariantId.HasValue)
            {
                var variant = await dbContext.ProductVariants
                    .FirstOrDefaultAsync(
                        productVariant => productVariant.Id == orderItem.ProductVariantId.Value,
                        cancellationToken);

                if (variant is null)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        "Product variant was not found.",
                        traceId));
                }

                var variantAvailableBefore = variant.QuantityAvailable;

                variant.QuantityAvailable += orderItem.Quantity;
                variant.UpdatedAt = now;

                await inventoryMovementService.LogAsync(
                    productId: orderItem.ProductId,
                    inventoryItemId: Guid.Empty,
                    type: InventoryMovementType.ReturnRestocked,
                    quantityChange: orderItem.Quantity,
                    quantityAvailableBefore: variantAvailableBefore,
                    quantityAvailableAfter: variant.QuantityAvailable,
                    quantityReservedBefore: variant.QuantityReserved,
                    quantityReservedAfter: variant.QuantityReserved,
                    reason: "Returned product variant order item was restocked.",
                    referenceType: nameof(ReturnRequest),
                    referenceId: returnRequest.Id.ToString(),
                    actorUserId: actorUserId,
                    createdAt: now,
                    cancellationToken: cancellationToken,
                    productVariantId: orderItem.ProductVariantId);

                restockedItems.Add(new
                {
                    orderItem.ProductId,
                    orderItem.ProductNameSnapshot,
                    orderItem.ProductVariantId,
                    orderItem.VariantNameSnapshot,
                    orderItem.Quantity,
                    QuantityAvailableBefore = variantAvailableBefore,
                    QuantityAvailableAfter = variant.QuantityAvailable
                });

                continue;
            }

            var inventoryItem = await dbContext.InventoryItems
                .FirstOrDefaultAsync(
                    item => item.ProductId == orderItem.ProductId,
                    cancellationToken);

            if (inventoryItem is null)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Inventory item was not found.",
                    traceId));
            }

            var quantityAvailableBefore = inventoryItem.QuantityAvailable;
            var quantityReservedBefore = inventoryItem.QuantityReserved;

            inventoryItem.QuantityAvailable += orderItem.Quantity;
            inventoryItem.UpdatedAt = now;

            await inventoryMovementService.LogAsync(
                productId: inventoryItem.ProductId,
                inventoryItemId: inventoryItem.Id,
                type: InventoryMovementType.ReturnRestocked,
                quantityChange: orderItem.Quantity,
                quantityAvailableBefore: quantityAvailableBefore,
                quantityAvailableAfter: inventoryItem.QuantityAvailable,
                quantityReservedBefore: quantityReservedBefore,
                quantityReservedAfter: inventoryItem.QuantityReserved,
                reason: "Returned order item was restocked.",
                referenceType: nameof(ReturnRequest),
                referenceId: returnRequest.Id.ToString(),
                actorUserId: actorUserId,
                createdAt: now,
                cancellationToken: cancellationToken);

            restockedItems.Add(new
            {
                orderItem.ProductId,
                orderItem.ProductNameSnapshot,
                orderItem.Quantity,
                InventoryItemId = inventoryItem.Id,
                QuantityAvailableBefore = quantityAvailableBefore,
                QuantityAvailableAfter = inventoryItem.QuantityAvailable
            });
        }

        returnRequest.Status = ReturnRequestStatus.Completed;
        returnRequest.CompletedAt = now;
        returnRequest.Order.Status = OrderStatus.Returned;

        await auditLogService.LogAsync(
            actorUserId: actorUserId,
            action: "ReturnCompleted",
            entityName: nameof(ReturnRequest),
            entityId: returnRequest.Id.ToString(),
            oldValue: new
            {
                ReturnRequest = oldValue,
                OrderStatus = previousOrderStatus.ToString()
            },
            newValue: new
            {
                ReturnRequest = ToAuditSnapshot(returnRequest),
                OrderStatus = returnRequest.Order.Status.ToString(),
                RestockedItems = restockedItems
            },
            reason: "Return request was completed and inventory was restocked.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(returnRequest));
    }

    private async Task<ReturnRequest?> LoadReturnRequestAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.ReturnRequests
            .Include(returnRequest => returnRequest.Order)
            .FirstOrDefaultAsync(
                returnRequest => returnRequest.Id == id,
                cancellationToken);
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    private static object ToAuditSnapshot(ReturnRequest returnRequest)
    {
        return new
        {
            returnRequest.Id,
            returnRequest.OrderId,
            returnRequest.UserId,
            returnRequest.Reason,
            Status = returnRequest.Status.ToString(),
            returnRequest.AdminNote,
            returnRequest.RequestedAt,
            returnRequest.ApprovedAt,
            returnRequest.RejectedAt,
            returnRequest.CompletedAt
        };
    }
}