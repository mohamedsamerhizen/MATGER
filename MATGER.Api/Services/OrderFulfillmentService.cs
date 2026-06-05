using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Orders;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class OrderFulfillmentService(
    ApplicationDbContext dbContext,
    IAuditLogService auditLogService,
    IInventoryMovementService inventoryMovementService) : IOrderFulfillmentService
{
    public async Task<ActionResult<OrderStateChangedResponse>> CancelAsync(
        Guid orderId,
        CancelOrderRequest? request,
        Guid actorUserId,
        bool canSeeAllOrders,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(order => order.InventoryReservations)
            .ThenInclude(reservation => reservation.ProductVariant)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (order is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Order was not found.",
                traceId));
        }

        if (!canSeeAllOrders && order.UserId != actorUserId)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Order was not found.",
                traceId));
        }

        if (!OrderStateMachine.CanTransition(order.Status, OrderStatus.Cancelled))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                $"Cannot transition order from {order.Status} to {OrderStatus.Cancelled}.",
                traceId));
        }

        var previousStatus = order.Status;
        var now = DateTime.UtcNow;
        var releasedReservations = new List<object>();

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = now;
        order.CancellationReason = NormalizeOptional(request?.Reason);

        foreach (var reservation in order.InventoryReservations)
        {
            if (reservation.Status != InventoryReservationStatus.Pending)
            {
                continue;
            }

            reservation.Status = InventoryReservationStatus.Released;
            reservation.ReleasedAt = now;

            if (reservation.ProductVariantId.HasValue)
            {
                var variant = await dbContext.ProductVariants
                    .FirstOrDefaultAsync(
                        productVariant => productVariant.Id == reservation.ProductVariantId.Value,
                        cancellationToken);

                if (variant is null)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        "Product variant was not found.",
                        traceId));
                }

                if (variant.QuantityReserved < reservation.Quantity)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        "Reserved variant quantity is invalid.",
                        traceId));
                }

                var variantAvailableBefore = variant.QuantityAvailable;
                var variantReservedBefore = variant.QuantityReserved;

                variant.QuantityReserved -= reservation.Quantity;
                variant.QuantityAvailable += reservation.Quantity;
                variant.UpdatedAt = now;

                await inventoryMovementService.LogAsync(
                    productId: reservation.ProductId,
                    inventoryItemId: Guid.Empty,
                    type: InventoryMovementType.ReservationReleased,
                    quantityChange: reservation.Quantity,
                    quantityAvailableBefore: variantAvailableBefore,
                    quantityAvailableAfter: variant.QuantityAvailable,
                    quantityReservedBefore: variantReservedBefore,
                    quantityReservedAfter: variant.QuantityReserved,
                    reason: "Product variant inventory reservation was released after order cancellation.",
                    referenceType: nameof(Order),
                    referenceId: order.Id.ToString(),
                    actorUserId: actorUserId,
                    createdAt: now,
                    cancellationToken: cancellationToken,
                    productVariantId: reservation.ProductVariantId);

                releasedReservations.Add(new
                {
                    reservation.Id,
                    reservation.ProductId,
                    reservation.ProductVariantId,
                    VariantName = variant.Name,
                    reservation.Quantity,
                    OldStatus = InventoryReservationStatus.Pending.ToString(),
                    NewStatus = reservation.Status.ToString(),
                    reservation.ReleasedAt,
                    QuantityAvailableBefore = variantAvailableBefore,
                    QuantityAvailableAfter = variant.QuantityAvailable,
                    QuantityReservedBefore = variantReservedBefore,
                    QuantityReservedAfter = variant.QuantityReserved
                });

                continue;
            }

            var inventoryItem = await dbContext.InventoryItems
                .FirstOrDefaultAsync(
                    item => item.ProductId == reservation.ProductId,
                    cancellationToken);

            if (inventoryItem is null)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Inventory item was not found.",
                    traceId));
            }

            if (inventoryItem.QuantityReserved < reservation.Quantity)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Reserved inventory quantity is invalid.",
                    traceId));
            }

            var quantityAvailableBefore = inventoryItem.QuantityAvailable;
            var quantityReservedBefore = inventoryItem.QuantityReserved;

            inventoryItem.QuantityReserved -= reservation.Quantity;
            inventoryItem.QuantityAvailable += reservation.Quantity;
            inventoryItem.UpdatedAt = now;

            await inventoryMovementService.LogAsync(
                productId: inventoryItem.ProductId,
                inventoryItemId: inventoryItem.Id,
                type: InventoryMovementType.ReservationReleased,
                quantityChange: reservation.Quantity,
                quantityAvailableBefore: quantityAvailableBefore,
                quantityAvailableAfter: inventoryItem.QuantityAvailable,
                quantityReservedBefore: quantityReservedBefore,
                quantityReservedAfter: inventoryItem.QuantityReserved,
                reason: "Inventory reservation was released after order cancellation.",
                referenceType: nameof(InventoryReservation),
                referenceId: reservation.Id.ToString(),
                actorUserId: actorUserId,
                createdAt: now,
                cancellationToken: cancellationToken);

            releasedReservations.Add(new
            {
                reservation.Id,
                reservation.ProductId,
                reservation.Quantity,
                OldStatus = InventoryReservationStatus.Pending.ToString(),
                NewStatus = reservation.Status.ToString(),
                reservation.ReleasedAt,
                InventoryItemId = inventoryItem.Id,
                QuantityAvailableBefore = quantityAvailableBefore,
                QuantityAvailableAfter = inventoryItem.QuantityAvailable,
                QuantityReservedBefore = quantityReservedBefore,
                QuantityReservedAfter = inventoryItem.QuantityReserved
            });
        }

        AddStatusHistory(
            order,
            previousStatus,
            order.Status,
            actorUserId,
            "Order was cancelled.",
            order.CancellationReason,
            now);

        await auditLogService.LogAsync(
            actorUserId: actorUserId,
            action: "OrderCancelled",
            entityName: nameof(Order),
            entityId: order.Id.ToString(),
            oldValue: new
            {
                Status = previousStatus.ToString()
            },
            newValue: new
            {
                Status = order.Status.ToString(),
                order.CancelledAt,
                order.CancellationReason,
                ReleasedReservations = releasedReservations
            },
            reason: "Order was cancelled and pending inventory reservations were released.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToStateChangedResponse(order, previousStatus, now));
    }

    public async Task<ActionResult<OrderStateChangedResponse>> MarkProcessingAsync(
        Guid orderId,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        return await ChangeStatusAsync(
            orderId,
            OrderStatus.Processing,
            actorUserId,
            traceId,
            cancellationToken: cancellationToken);
    }

    public async Task<ActionResult<OrderStateChangedResponse>> MarkShippedAsync(
        Guid orderId,
        ShipOrderRequest? request,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        return await ChangeStatusAsync(
            orderId,
            OrderStatus.Shipped,
            actorUserId,
            traceId,
            shipOrderRequest: request,
            cancellationToken: cancellationToken);
    }

    public async Task<ActionResult<OrderStateChangedResponse>> MarkDeliveredAsync(
        Guid orderId,
        DeliverOrderRequest? request,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        return await ChangeStatusAsync(
            orderId,
            OrderStatus.Delivered,
            actorUserId,
            traceId,
            deliverOrderRequest: request,
            cancellationToken: cancellationToken);
    }

    private async Task<ActionResult<OrderStateChangedResponse>> ChangeStatusAsync(
        Guid orderId,
        OrderStatus targetStatus,
        Guid actorUserId,
        string traceId,
        ShipOrderRequest? shipOrderRequest = null,
        DeliverOrderRequest? deliverOrderRequest = null,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (order is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Order was not found.",
                traceId));
        }

        if (!OrderStateMachine.CanTransition(order.Status, targetStatus))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                $"Cannot transition order from {order.Status} to {targetStatus}.",
                traceId));
        }

        var previousStatus = order.Status;
        var now = DateTime.UtcNow;

        order.Status = targetStatus;

        if (targetStatus == OrderStatus.Shipped)
        {
            order.ShippedAt = now;
            order.ShippingStatus = ShippingStatus.Shipped;
            order.ShippingCarrier = NormalizeOptional(shipOrderRequest?.ShippingCarrier);
            order.TrackingNumber = NormalizeOptional(shipOrderRequest?.TrackingNumber);
        }

        if (targetStatus == OrderStatus.Delivered)
        {
            order.DeliveredAt = now;
            order.ShippingStatus = ShippingStatus.Delivered;
            order.DeliveryNote = NormalizeOptional(deliverOrderRequest?.DeliveryNote);
        }

        AddStatusHistory(
            order,
            previousStatus,
            order.Status,
            actorUserId,
            GetStatusHistoryReason(targetStatus),
            GetStatusHistoryNote(targetStatus, shipOrderRequest, deliverOrderRequest),
            now);

        if (targetStatus == OrderStatus.Processing)
        {
            await auditLogService.LogAsync(
                actorUserId: actorUserId,
                action: "OrderProcessingStarted",
                entityName: nameof(Order),
                entityId: order.Id.ToString(),
                oldValue: new
                {
                    Status = previousStatus.ToString()
                },
                newValue: new
                {
                    Status = order.Status.ToString(),
                    OrderId = order.Id,
                    order.OrderNumber,
                    ChangedAt = now
                },
                reason: "Order processing was started.",
                cancellationToken: cancellationToken);
        }

        if (targetStatus == OrderStatus.Shipped || targetStatus == OrderStatus.Delivered)
        {
            await auditLogService.LogAsync(
                actorUserId: actorUserId,
                action: targetStatus == OrderStatus.Shipped
                    ? "OrderShipped"
                    : "OrderDelivered",
                entityName: nameof(Order),
                entityId: order.Id.ToString(),
                oldValue: new
                {
                    Status = previousStatus.ToString()
                },
                newValue: new
                {
                    Status = order.Status.ToString(),
                    order.ShippedAt,
                    order.DeliveredAt,
                    order.ShippingCarrier,
                    order.TrackingNumber,
                    order.DeliveryNote
                },
                reason: targetStatus == OrderStatus.Shipped
                    ? "Order was marked as shipped."
                    : "Order was marked as delivered.",
                cancellationToken: cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToStateChangedResponse(order, previousStatus, now));
    }


    private void AddStatusHistory(
        Order order,
        OrderStatus previousStatus,
        OrderStatus newStatus,
        Guid actorUserId,
        string reason,
        string? note,
        DateTime createdAt)
    {
        dbContext.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedByUserId = actorUserId,
            Reason = reason,
            Note = NormalizeOptional(note),
            CreatedAt = createdAt
        });
    }

    private static string GetStatusHistoryReason(OrderStatus targetStatus)
    {
        return targetStatus switch
        {
            OrderStatus.Processing => "Order processing was started.",
            OrderStatus.Shipped => "Order was marked as shipped.",
            OrderStatus.Delivered => "Order was marked as delivered.",
            _ => "Order status was changed."
        };
    }

    private static string? GetStatusHistoryNote(
        OrderStatus targetStatus,
        ShipOrderRequest? shipOrderRequest,
        DeliverOrderRequest? deliverOrderRequest)
    {
        return targetStatus switch
        {
            OrderStatus.Shipped => NormalizeOptional(
                string.Join(
                    " | ",
                    new[]
                    {
                        NormalizeOptional(shipOrderRequest?.ShippingCarrier) is null
                            ? null
                            : $"Carrier: {NormalizeOptional(shipOrderRequest?.ShippingCarrier)}",
                        NormalizeOptional(shipOrderRequest?.TrackingNumber) is null
                            ? null
                            : $"Tracking: {NormalizeOptional(shipOrderRequest?.TrackingNumber)}"
                    }.Where(value => value is not null))),
            OrderStatus.Delivered => NormalizeOptional(deliverOrderRequest?.DeliveryNote),
            _ => null
        };
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

    private static OrderStateChangedResponse ToStateChangedResponse(
        Order order,
        OrderStatus previousStatus,
        DateTime changedAt)
    {
        return new OrderStateChangedResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            PreviousStatus = previousStatus.ToString(),
            CurrentStatus = order.Status.ToString(),
            ChangedAt = changedAt,
            CancellationReason = order.CancellationReason,
            ShippingCarrier = order.ShippingCarrier,
            TrackingNumber = order.TrackingNumber,
            DeliveryNote = order.DeliveryNote
        };
    }
}