using System.Data;
using MATGER.Api.Data;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class ExpiredInventoryReservationService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ExpiredInventoryReservationService> logger,
    IHostEnvironment environment) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan DevelopmentInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = environment.IsDevelopment()
            ? DevelopmentInterval
            : DefaultInterval;

        logger.LogInformation(
            "Expired inventory reservation service started with interval {Interval}.",
            interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunExpirationBatchAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunExpirationBatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var inventoryMovementService = scope.ServiceProvider.GetRequiredService<IInventoryMovementService>();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                stoppingToken);

            var now = DateTime.UtcNow;

            var reservations = await dbContext.InventoryReservations
                .Include(reservation => reservation.Order)
                .ThenInclude(order => order.InventoryReservations)
                .Include(reservation => reservation.ProductVariant)
                .Where(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending &&
                    reservation.ExpiresAt <= now &&
                    reservation.Order.Status == OrderStatus.PendingPayment)
                .OrderBy(reservation => reservation.ExpiresAt)
                .Take(BatchSize)
                .ToListAsync(stoppingToken);

            if (reservations.Count == 0)
            {
                await transaction.CommitAsync(stoppingToken);

                logger.LogDebug("No expired inventory reservations were found.");

                return;
            }

            var productIds = reservations
                .Where(reservation => reservation.ProductVariantId is null)
                .Select(reservation => reservation.ProductId)
                .Distinct()
                .ToList();

            var inventoryItems = await dbContext.InventoryItems
                .Where(inventoryItem => productIds.Contains(inventoryItem.ProductId))
                .ToDictionaryAsync(inventoryItem => inventoryItem.ProductId, stoppingToken);

            var affectedOrders = new Dictionary<Guid, Order>();
            var expiredReservationIds = new HashSet<Guid>();
            var expiredReservationCount = 0;

            foreach (var reservation in reservations)
            {
                if (reservation.Order.Status != OrderStatus.PendingPayment)
                {
                    logger.LogWarning(
                        "Skipping expired reservation {ReservationId} because order {OrderId} is no longer pending payment. Current order status: {OrderStatus}.",
                        reservation.Id,
                        reservation.OrderId,
                        reservation.Order.Status);

                    continue;
                }

                if (reservation.ProductVariantId is not null)
                {
                    if (reservation.ProductVariant is null)
                    {
                        logger.LogError(
                            "Product variant was not found while expiring reservation {ReservationId} for variant {ProductVariantId}.",
                            reservation.Id,
                            reservation.ProductVariantId);

                        continue;
                    }

                    var quantityAvailableBefore = reservation.ProductVariant.QuantityAvailable;
                    var quantityReservedBefore = reservation.ProductVariant.QuantityReserved;
                    var quantityToRelease = Math.Min(quantityReservedBefore, reservation.Quantity);
                    var previousReservationStatus = reservation.Status;

                    if (quantityToRelease < reservation.Quantity)
                    {
                        logger.LogWarning(
                            "Variant reservation {ReservationId} had less reserved stock than expected. Reserved: {QuantityReserved}, reservation quantity: {ReservationQuantity}. Releasing available reserved stock only.",
                            reservation.Id,
                            quantityReservedBefore,
                            reservation.Quantity);
                    }

                    reservation.Status = InventoryReservationStatus.Expired;
                    reservation.ExpiredAt = now;

                    reservation.ProductVariant.QuantityReserved -= quantityToRelease;
                    reservation.ProductVariant.QuantityAvailable += quantityToRelease;
                    reservation.ProductVariant.UpdatedAt = now;

                    if (quantityToRelease > 0)
                    {
                        await inventoryMovementService.LogAsync(
                            productId: reservation.ProductId,
                            inventoryItemId: Guid.Empty,
                            type: InventoryMovementType.ReservationExpired,
                            quantityChange: quantityToRelease,
                            quantityAvailableBefore: quantityAvailableBefore,
                            quantityAvailableAfter: reservation.ProductVariant.QuantityAvailable,
                            quantityReservedBefore: quantityReservedBefore,
                            quantityReservedAfter: reservation.ProductVariant.QuantityReserved,
                            reason: "Product variant inventory reservation expired before payment confirmation.",
                            referenceType: nameof(InventoryReservation),
                            referenceId: reservation.Id.ToString(),
                            actorUserId: null,
                            createdAt: now,
                            cancellationToken: stoppingToken,
                            productVariantId: reservation.ProductVariantId);
                    }

                    affectedOrders[reservation.OrderId] = reservation.Order;
                    expiredReservationIds.Add(reservation.Id);
                    expiredReservationCount++;

                    await auditLogService.LogAsync(
                        actorUserId: null,
                        action: "ProductVariantReservationExpired",
                        entityName: nameof(InventoryReservation),
                        entityId: reservation.Id.ToString(),
                        oldValue: new
                        {
                            Status = previousReservationStatus.ToString(),
                            reservation.ExpiresAt,
                            reservation.ProductVariantId,
                            QuantityAvailable = quantityAvailableBefore,
                            QuantityReserved = quantityReservedBefore
                        },
                        newValue: new
                        {
                            Status = reservation.Status.ToString(),
                            reservation.ExpiredAt,
                            reservation.OrderId,
                            reservation.ProductId,
                            reservation.ProductVariantId,
                            reservation.Quantity,
                            ReleasedQuantity = quantityToRelease,
                            QuantityAvailable = reservation.ProductVariant.QuantityAvailable,
                            QuantityReserved = reservation.ProductVariant.QuantityReserved
                        },
                        reason: "Pending product variant reservation expired before payment was confirmed.",
                        cancellationToken: stoppingToken);

                    continue;
                }

                if (!inventoryItems.TryGetValue(reservation.ProductId, out var inventoryItem))
                {
                    logger.LogError(
                        "Inventory item was not found while expiring reservation {ReservationId} for product {ProductId}.",
                        reservation.Id,
                        reservation.ProductId);

                    continue;
                }

                var productQuantityAvailableBefore = inventoryItem.QuantityAvailable;
                var productQuantityReservedBefore = inventoryItem.QuantityReserved;
                var productQuantityToRelease = Math.Min(productQuantityReservedBefore, reservation.Quantity);
                var previousProductReservationStatus = reservation.Status;

                if (productQuantityToRelease < reservation.Quantity)
                {
                    logger.LogWarning(
                        "Inventory reservation {ReservationId} had less reserved stock than expected. Reserved: {QuantityReserved}, reservation quantity: {ReservationQuantity}. Releasing available reserved stock only.",
                        reservation.Id,
                        productQuantityReservedBefore,
                        reservation.Quantity);
                }

                reservation.Status = InventoryReservationStatus.Expired;
                reservation.ExpiredAt = now;

                inventoryItem.QuantityReserved -= productQuantityToRelease;
                inventoryItem.QuantityAvailable += productQuantityToRelease;
                inventoryItem.UpdatedAt = now;

                if (productQuantityToRelease > 0)
                {
                    await inventoryMovementService.LogAsync(
                        productId: inventoryItem.ProductId,
                        inventoryItemId: inventoryItem.Id,
                        type: InventoryMovementType.ReservationExpired,
                        quantityChange: productQuantityToRelease,
                        quantityAvailableBefore: productQuantityAvailableBefore,
                        quantityAvailableAfter: inventoryItem.QuantityAvailable,
                        quantityReservedBefore: productQuantityReservedBefore,
                        quantityReservedAfter: inventoryItem.QuantityReserved,
                        reason: "Inventory reservation expired before payment confirmation.",
                        referenceType: nameof(InventoryReservation),
                        referenceId: reservation.Id.ToString(),
                        actorUserId: null,
                        createdAt: now,
                        cancellationToken: stoppingToken);
                }

                affectedOrders[reservation.OrderId] = reservation.Order;
                expiredReservationIds.Add(reservation.Id);
                expiredReservationCount++;

                await auditLogService.LogAsync(
                    actorUserId: null,
                    action: "InventoryReservationExpired",
                    entityName: nameof(InventoryReservation),
                    entityId: reservation.Id.ToString(),
                    oldValue: new
                    {
                        Status = previousProductReservationStatus.ToString(),
                        reservation.ExpiresAt,
                        InventoryItemId = inventoryItem.Id,
                        QuantityAvailable = productQuantityAvailableBefore,
                        QuantityReserved = productQuantityReservedBefore
                    },
                    newValue: new
                    {
                        Status = reservation.Status.ToString(),
                        reservation.ExpiredAt,
                        reservation.OrderId,
                        reservation.ProductId,
                        reservation.Quantity,
                        ReleasedQuantity = productQuantityToRelease,
                        InventoryItemId = inventoryItem.Id,
                        QuantityAvailable = inventoryItem.QuantityAvailable,
                        QuantityReserved = inventoryItem.QuantityReserved
                    },
                    reason: "Pending inventory reservation expired before payment was confirmed.",
                    cancellationToken: stoppingToken);
            }

            var paymentFailedOrderCount = 0;

            foreach (var order in affectedOrders.Values)
            {
                if (order.Status != OrderStatus.PendingPayment)
                {
                    continue;
                }

                if (order.InventoryReservations.Any(reservation =>
                        reservation.Status == InventoryReservationStatus.Pending))
                {
                    continue;
                }

                var previousOrderStatus = order.Status;

                order.Status = OrderStatus.PaymentFailed;
                paymentFailedOrderCount++;

                await auditLogService.LogAsync(
                    actorUserId: null,
                    action: "OrderMarkedPaymentFailedDueToExpiredReservation",
                    entityName: nameof(Order),
                    entityId: order.Id.ToString(),
                    oldValue: new
                    {
                        Status = previousOrderStatus.ToString(),
                        order.OrderNumber,
                        order.Total
                    },
                    newValue: new
                    {
                        Status = order.Status.ToString(),
                        order.OrderNumber,
                        order.Total,
                        ExpiredReservations = order.InventoryReservations
                            .Where(reservation => expiredReservationIds.Contains(reservation.Id))
                            .Select(reservation => new
                            {
                                reservation.Id,
                                reservation.ProductId,
                                reservation.ProductVariantId,
                                reservation.Quantity,
                                Status = reservation.Status.ToString(),
                                reservation.ExpiredAt
                            })
                            .ToList()
                    },
                    reason: "Order was marked payment failed because all pending inventory reservations expired.",
                    cancellationToken: stoppingToken);
            }

            if (expiredReservationCount == 0)
            {
                await transaction.CommitAsync(stoppingToken);

                logger.LogWarning(
                    "Expired inventory reservation batch found {ReservationCount} candidate reservations, but none could be processed.",
                    reservations.Count);

                return;
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            await transaction.CommitAsync(stoppingToken);

            logger.LogInformation(
                "Expired {ExpiredReservationCount} inventory reservation(s) and marked {PaymentFailedOrderCount} order(s) as payment failed.",
                expiredReservationCount,
                paymentFailedOrderCount);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Expired inventory reservation service failed while processing a batch.");
        }
    }
}
