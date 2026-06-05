using System.Data;
using MATGER.Api.Data;
using MATGER.Api.DTOs.CheckoutConsistency;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Helpers;
using MATGER.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class CheckoutConsistencyService(
    ApplicationDbContext dbContext,
    IAuditLogService auditLogService,
    IInventoryMovementService inventoryMovementService,
    ILogger<CheckoutConsistencyService> logger) : ICheckoutConsistencyService
{
    private const int MaintenanceBatchSize = 250;

    private static readonly OrderStatus[] PaidOrAfterPaidStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Processing,
        OrderStatus.Shipped,
        OrderStatus.Delivered,
        OrderStatus.ReturnRequested,
        OrderStatus.Returned,
        OrderStatus.Refunded
    ];

    public async Task<CheckoutConsistencySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var pendingPaymentOrdersCount = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order => order.Status == OrderStatus.PendingPayment, cancellationToken);

        var expiredPendingReservationsCount = await dbContext.InventoryReservations
            .AsNoTracking()
            .CountAsync(reservation =>
                reservation.Status == InventoryReservationStatus.Pending &&
                reservation.ExpiresAt <= now &&
                reservation.Order.Status == OrderStatus.PendingPayment,
                cancellationToken);

        var pendingPaymentOrdersWithExpiredReservationsCount = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order =>
                order.Status == OrderStatus.PendingPayment &&
                order.InventoryReservations.Any(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending &&
                    reservation.ExpiresAt <= now),
                cancellationToken);

        var pendingPaymentOrdersWithoutReservationsCount = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order =>
                order.Status == OrderStatus.PendingPayment &&
                !order.InventoryReservations.Any(),
                cancellationToken);

        var paymentFailedOrdersWithPendingReservationsCount = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order =>
                order.Status == OrderStatus.PaymentFailed &&
                order.InventoryReservations.Any(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending),
                cancellationToken);

        var paidOrdersWithPendingReservationsCount = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order =>
                PaidOrAfterPaidStatuses.Contains(order.Status) &&
                order.InventoryReservations.Any(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending),
                cancellationToken);

        var paidOrdersWithoutSucceededPaymentCount = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(order =>
                PaidOrAfterPaidStatuses.Contains(order.Status) &&
                !order.Payments.Any(payment => payment.Status == PaymentStatus.Succeeded),
                cancellationToken);

        var succeededPaymentsWithInvalidOrderStatusCount = await dbContext.Payments
            .AsNoTracking()
            .CountAsync(payment =>
                payment.Status == PaymentStatus.Succeeded &&
                !PaidOrAfterPaidStatuses.Contains(payment.Order.Status),
                cancellationToken);

        var productsWithReservedQuantityMismatchCount = await CountProductsWithReservedQuantityMismatchAsync(
            cancellationToken);

        var productsWithNegativeInventoryCount = await dbContext.InventoryItems
            .AsNoTracking()
            .CountAsync(inventoryItem =>
                inventoryItem.QuantityAvailable < 0 ||
                inventoryItem.QuantityReserved < 0,
                cancellationToken);

        var criticalIssuesCount =
            pendingPaymentOrdersWithoutReservationsCount +
            paymentFailedOrdersWithPendingReservationsCount +
            paidOrdersWithPendingReservationsCount +
            paidOrdersWithoutSucceededPaymentCount +
            succeededPaymentsWithInvalidOrderStatusCount +
            productsWithReservedQuantityMismatchCount +
            productsWithNegativeInventoryCount;

        var warningIssuesCount = pendingPaymentOrdersWithExpiredReservationsCount;
        var openIssuesCount = criticalIssuesCount + warningIssuesCount;

        return new CheckoutConsistencySummaryResponse
        {
            GeneratedAt = now,
            HealthStatus = openIssuesCount == 0
                ? "Healthy"
                : criticalIssuesCount > 0
                    ? "Critical"
                    : "Warning",
            OpenIssuesCount = openIssuesCount,
            CriticalIssuesCount = criticalIssuesCount,
            WarningIssuesCount = warningIssuesCount,
            PendingPaymentOrdersCount = pendingPaymentOrdersCount,
            ExpiredPendingReservationsCount = expiredPendingReservationsCount,
            PendingPaymentOrdersWithExpiredReservationsCount = pendingPaymentOrdersWithExpiredReservationsCount,
            PendingPaymentOrdersWithoutReservationsCount = pendingPaymentOrdersWithoutReservationsCount,
            PaymentFailedOrdersWithPendingReservationsCount = paymentFailedOrdersWithPendingReservationsCount,
            PaidOrdersWithPendingReservationsCount = paidOrdersWithPendingReservationsCount,
            PaidOrdersWithoutSucceededPaymentCount = paidOrdersWithoutSucceededPaymentCount,
            SucceededPaymentsWithInvalidOrderStatusCount = succeededPaymentsWithInvalidOrderStatusCount,
            ProductsWithReservedQuantityMismatchCount = productsWithReservedQuantityMismatchCount,
            ProductsWithNegativeInventoryCount = productsWithNegativeInventoryCount
        };
    }

    public async Task<PaginatedResponse<CheckoutConsistencyIssueResponse>> GetIssuesAsync(
        string? severity,
        string? issueType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedSeverity = NormalizeOptional(severity);
        var normalizedIssueType = NormalizeOptional(issueType);
        var (normalizedPage, normalizedPageSize) = PaginationHelper.Normalize(page, pageSize);
        var now = DateTime.UtcNow;

        var issues = new List<CheckoutConsistencyIssueResponse>();

        issues.AddRange(await GetPendingPaymentOrdersWithExpiredReservationsAsync(now, cancellationToken));
        issues.AddRange(await GetPendingPaymentOrdersWithoutReservationsAsync(now, cancellationToken));
        issues.AddRange(await GetPaymentFailedOrdersWithPendingReservationsAsync(now, cancellationToken));
        issues.AddRange(await GetPaidOrdersWithPendingReservationsAsync(now, cancellationToken));
        issues.AddRange(await GetPaidOrdersWithoutSucceededPaymentAsync(now, cancellationToken));
        issues.AddRange(await GetSucceededPaymentsWithInvalidOrderStatusAsync(now, cancellationToken));
        issues.AddRange(await GetProductsWithReservedQuantityMismatchAsync(now, cancellationToken));
        issues.AddRange(await GetProductsWithNegativeInventoryAsync(now, cancellationToken));

        if (normalizedSeverity is not null)
        {
            issues = issues
                .Where(issue => string.Equals(
                    issue.Severity,
                    normalizedSeverity,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (normalizedIssueType is not null)
        {
            issues = issues
                .Where(issue => string.Equals(
                    issue.IssueType,
                    normalizedIssueType,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        issues = issues
            .OrderByDescending(issue => GetSeverityRank(issue.Severity))
            .ThenBy(issue => issue.IssueType)
            .ThenBy(issue => issue.OrderNumber)
            .ThenBy(issue => issue.ProductName)
            .ToList();

        var totalCount = issues.Count;

        var items = issues
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return PaginatedResponse<CheckoutConsistencyIssueResponse>.Create(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    public async Task<CheckoutMaintenanceRunResponse> ExpirePendingPaymentsAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var notes = new List<string>();
        var expiredReservationIds = new List<Guid>();
        var affectedOrderIds = new HashSet<Guid>();
        var expiredReservationCount = 0;
        var paymentFailedOrderCount = 0;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var expiredReservations = await dbContext.InventoryReservations
            .Include(reservation => reservation.Order)
            .ThenInclude(order => order.InventoryReservations)
            .Include(reservation => reservation.ProductVariant)
            .Where(reservation =>
                reservation.Status == InventoryReservationStatus.Pending &&
                reservation.ExpiresAt <= startedAt &&
                reservation.Order.Status == OrderStatus.PendingPayment)
            .OrderBy(reservation => reservation.ExpiresAt)
            .Take(MaintenanceBatchSize)
            .ToListAsync(cancellationToken);

        if (expiredReservations.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);

            return new CheckoutMaintenanceRunResponse
            {
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                Notes = ["No expired pending payment reservations were found."]
            };
        }

        var productIds = expiredReservations
            .Where(reservation => reservation.ProductVariantId is null)
            .Select(reservation => reservation.ProductId)
            .Distinct()
            .ToList();

        var inventoryItems = await dbContext.InventoryItems
            .Where(inventoryItem => productIds.Contains(inventoryItem.ProductId))
            .ToDictionaryAsync(inventoryItem => inventoryItem.ProductId, cancellationToken);

        var affectedOrders = new Dictionary<Guid, Order>();

        foreach (var reservation in expiredReservations)
        {
            if (reservation.Order.Status != OrderStatus.PendingPayment)
            {
                notes.Add($"Skipped reservation {reservation.Id} because order {reservation.OrderId} is not pending payment.");
                continue;
            }

            if (reservation.ProductVariantId is not null)
            {
                if (reservation.ProductVariant is null)
                {
                    notes.Add($"Skipped reservation {reservation.Id} because product variant {reservation.ProductVariantId} was not found.");
                    continue;
                }

                if (reservation.ProductVariant.QuantityReserved < reservation.Quantity)
                {
                    notes.Add($"Skipped reservation {reservation.Id} because variant reserved quantity is lower than reservation quantity.");
                    continue;
                }

                var variantAvailableBefore = reservation.ProductVariant.QuantityAvailable;
                var variantReservedBefore = reservation.ProductVariant.QuantityReserved;
                var previousVariantReservationStatus = reservation.Status;

                reservation.Status = InventoryReservationStatus.Expired;
                reservation.ExpiredAt = startedAt;

                reservation.ProductVariant.QuantityReserved -= reservation.Quantity;
                reservation.ProductVariant.QuantityAvailable += reservation.Quantity;
                reservation.ProductVariant.UpdatedAt = startedAt;

                await inventoryMovementService.LogAsync(
                    productId: reservation.ProductId,
                    inventoryItemId: Guid.Empty,
                    type: InventoryMovementType.ReservationExpired,
                    quantityChange: reservation.Quantity,
                    quantityAvailableBefore: variantAvailableBefore,
                    quantityAvailableAfter: reservation.ProductVariant.QuantityAvailable,
                    quantityReservedBefore: variantReservedBefore,
                    quantityReservedAfter: reservation.ProductVariant.QuantityReserved,
                    reason: "Expired product variant reservation was released by admin checkout consistency maintenance.",
                    referenceType: nameof(InventoryReservation),
                    referenceId: reservation.Id.ToString(),
                    actorUserId: actorUserId,
                    createdAt: startedAt,
                    cancellationToken: cancellationToken,
                    productVariantId: reservation.ProductVariantId);

                await auditLogService.LogAsync(
                    actorUserId: actorUserId,
                    action: "CheckoutVariantReservationExpiredByAdminMaintenance",
                    entityName: nameof(InventoryReservation),
                    entityId: reservation.Id.ToString(),
                    oldValue: new
                    {
                        Status = previousVariantReservationStatus.ToString(),
                        reservation.ExpiresAt,
                        reservation.ProductVariantId,
                        QuantityAvailable = variantAvailableBefore,
                        QuantityReserved = variantReservedBefore
                    },
                    newValue: new
                    {
                        Status = reservation.Status.ToString(),
                        reservation.ExpiredAt,
                        reservation.OrderId,
                        reservation.ProductId,
                        reservation.ProductVariantId,
                        reservation.Quantity,
                        QuantityAvailable = reservation.ProductVariant.QuantityAvailable,
                        QuantityReserved = reservation.ProductVariant.QuantityReserved
                    },
                    reason: "Admin maintenance expired a pending payment product variant reservation.",
                    cancellationToken: cancellationToken);

                affectedOrders[reservation.OrderId] = reservation.Order;
                affectedOrderIds.Add(reservation.OrderId);
                expiredReservationIds.Add(reservation.Id);
                expiredReservationCount++;

                continue;
            }

            if (!inventoryItems.TryGetValue(reservation.ProductId, out var inventoryItem))
            {
                notes.Add($"Skipped reservation {reservation.Id} because inventory item for product {reservation.ProductId} was not found.");
                continue;
            }

            if (inventoryItem.QuantityReserved < reservation.Quantity)
            {
                notes.Add($"Skipped reservation {reservation.Id} because reserved quantity is lower than reservation quantity.");
                continue;
            }

            var quantityAvailableBefore = inventoryItem.QuantityAvailable;
            var quantityReservedBefore = inventoryItem.QuantityReserved;
            var previousReservationStatus = reservation.Status;

            reservation.Status = InventoryReservationStatus.Expired;
            reservation.ExpiredAt = startedAt;

            inventoryItem.QuantityReserved -= reservation.Quantity;
            inventoryItem.QuantityAvailable += reservation.Quantity;
            inventoryItem.UpdatedAt = startedAt;

            await inventoryMovementService.LogAsync(
                productId: inventoryItem.ProductId,
                inventoryItemId: inventoryItem.Id,
                type: InventoryMovementType.ReservationExpired,
                quantityChange: reservation.Quantity,
                quantityAvailableBefore: quantityAvailableBefore,
                quantityAvailableAfter: inventoryItem.QuantityAvailable,
                quantityReservedBefore: quantityReservedBefore,
                quantityReservedAfter: inventoryItem.QuantityReserved,
                reason: "Expired pending payment reservation was released by admin checkout consistency maintenance.",
                referenceType: nameof(InventoryReservation),
                referenceId: reservation.Id.ToString(),
                actorUserId: actorUserId,
                createdAt: startedAt,
                cancellationToken: cancellationToken);

            await auditLogService.LogAsync(
                actorUserId: actorUserId,
                action: "CheckoutReservationExpiredByAdminMaintenance",
                entityName: nameof(InventoryReservation),
                entityId: reservation.Id.ToString(),
                oldValue: new
                {
                    Status = previousReservationStatus.ToString(),
                    reservation.ExpiresAt,
                    InventoryItemId = inventoryItem.Id,
                    QuantityAvailable = quantityAvailableBefore,
                    QuantityReserved = quantityReservedBefore
                },
                newValue: new
                {
                    Status = reservation.Status.ToString(),
                    reservation.ExpiredAt,
                    reservation.OrderId,
                    reservation.ProductId,
                    reservation.Quantity,
                    InventoryItemId = inventoryItem.Id,
                    QuantityAvailable = inventoryItem.QuantityAvailable,
                    QuantityReserved = inventoryItem.QuantityReserved
                },
                reason: "Admin maintenance expired a pending payment reservation.",
                cancellationToken: cancellationToken);

            affectedOrders[reservation.OrderId] = reservation.Order;
            affectedOrderIds.Add(reservation.OrderId);
            expiredReservationIds.Add(reservation.Id);
            expiredReservationCount++;
        }

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

            dbContext.OrderStatusHistories.Add(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Order = order,
                PreviousStatus = previousOrderStatus,
                NewStatus = order.Status,
                ChangedByUserId = actorUserId,
                Reason = "Order payment window expired.",
                Note = "All pending inventory reservations were expired by admin checkout consistency maintenance.",
                CreatedAt = startedAt
            });

            await auditLogService.LogAsync(
                actorUserId: actorUserId,
                action: "OrderMarkedPaymentFailedByCheckoutMaintenance",
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
                    ChangedAt = startedAt
                },
                reason: "Admin maintenance marked the order as payment failed after expired reservations were released.",
                cancellationToken: cancellationToken);

            paymentFailedOrderCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Checkout maintenance expired {ExpiredReservationCount} reservations and marked {PaymentFailedOrderCount} orders as payment failed.",
            expiredReservationCount,
            paymentFailedOrderCount);

        if (expiredReservations.Count == MaintenanceBatchSize)
        {
            notes.Add($"Batch limit of {MaintenanceBatchSize} expired reservations was reached. Run maintenance again to continue.");
        }

        return new CheckoutMaintenanceRunResponse
        {
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            ExpiredReservationsCount = expiredReservationCount,
            PaymentFailedOrdersCount = paymentFailedOrderCount,
            AffectedOrderIds = affectedOrderIds.OrderBy(id => id).ToList(),
            ExpiredReservationIds = expiredReservationIds.OrderBy(id => id).ToList(),
            Notes = notes
        };
    }

    private async Task<int> CountProductsWithReservedQuantityMismatchAsync(
        CancellationToken cancellationToken)
    {
        var pendingReservationQuantities = dbContext.InventoryReservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.Status == InventoryReservationStatus.Pending &&
                reservation.ProductVariantId == null)
            .GroupBy(reservation => reservation.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                ExpectedReservedQuantity = group.Sum(reservation => reservation.Quantity)
            });

        return await dbContext.InventoryItems
            .AsNoTracking()
            .GroupJoin(
                pendingReservationQuantities,
                inventoryItem => inventoryItem.ProductId,
                reservationQuantity => reservationQuantity.ProductId,
                (inventoryItem, reservationQuantities) => new
                {
                    inventoryItem.QuantityReserved,
                    ExpectedReservedQuantity = reservationQuantities
                        .Select(reservationQuantity => reservationQuantity.ExpectedReservedQuantity)
                        .FirstOrDefault()
                })
            .CountAsync(item => item.QuantityReserved != item.ExpectedReservedQuantity, cancellationToken);
    }

    private async Task<IReadOnlyList<CheckoutConsistencyIssueResponse>> GetPendingPaymentOrdersWithExpiredReservationsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == OrderStatus.PendingPayment &&
                order.InventoryReservations.Any(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending &&
                    reservation.ExpiresAt <= now))
            .Select(order => new CheckoutConsistencyIssueResponse
            {
                IssueType = "PendingPaymentExpiredReservations",
                Severity = "Warning",
                Message = "Pending payment order has expired inventory reservations.",
                RecommendedAction = "Run checkout consistency maintenance to expire reservations and release inventory.",
                DetectedAt = now,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderStatus = order.Status.ToString(),
                OrderTotal = order.Total,
                PendingReservationsCount = order.InventoryReservations.Count(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending &&
                    reservation.ExpiresAt <= now),
                OldestReservationExpiresAt = order.InventoryReservations
                    .Where(reservation =>
                        reservation.Status == InventoryReservationStatus.Pending &&
                        reservation.ExpiresAt <= now)
                    .Min(reservation => (DateTime?)reservation.ExpiresAt)
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CheckoutConsistencyIssueResponse>> GetPendingPaymentOrdersWithoutReservationsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == OrderStatus.PendingPayment &&
                !order.InventoryReservations.Any())
            .Select(order => new CheckoutConsistencyIssueResponse
            {
                IssueType = "PendingPaymentWithoutReservations",
                Severity = "Critical",
                Message = "Pending payment order has no inventory reservations.",
                RecommendedAction = "Investigate checkout creation logs. This state should not happen after a valid checkout start.",
                DetectedAt = now,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderStatus = order.Status.ToString(),
                OrderTotal = order.Total
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CheckoutConsistencyIssueResponse>> GetPaymentFailedOrdersWithPendingReservationsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == OrderStatus.PaymentFailed &&
                order.InventoryReservations.Any(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending))
            .Select(order => new CheckoutConsistencyIssueResponse
            {
                IssueType = "PaymentFailedWithPendingReservations",
                Severity = "Critical",
                Message = "Payment failed order still has pending inventory reservations.",
                RecommendedAction = "Release pending reservations and review failed payment handling.",
                DetectedAt = now,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderStatus = order.Status.ToString(),
                OrderTotal = order.Total,
                PendingReservationsCount = order.InventoryReservations.Count(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending),
                OldestReservationExpiresAt = order.InventoryReservations
                    .Where(reservation => reservation.Status == InventoryReservationStatus.Pending)
                    .Min(reservation => (DateTime?)reservation.ExpiresAt)
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CheckoutConsistencyIssueResponse>> GetPaidOrdersWithPendingReservationsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                PaidOrAfterPaidStatuses.Contains(order.Status) &&
                order.InventoryReservations.Any(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending))
            .Select(order => new CheckoutConsistencyIssueResponse
            {
                IssueType = "PaidOrderWithPendingReservations",
                Severity = "Critical",
                Message = "Paid or fulfilled order still has pending reservations instead of confirmed reservations.",
                RecommendedAction = "Review payment confirmation inventory transition and audit inventory quantities.",
                DetectedAt = now,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderStatus = order.Status.ToString(),
                OrderTotal = order.Total,
                PendingReservationsCount = order.InventoryReservations.Count(reservation =>
                    reservation.Status == InventoryReservationStatus.Pending),
                OldestReservationExpiresAt = order.InventoryReservations
                    .Where(reservation => reservation.Status == InventoryReservationStatus.Pending)
                    .Min(reservation => (DateTime?)reservation.ExpiresAt)
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CheckoutConsistencyIssueResponse>> GetPaidOrdersWithoutSucceededPaymentAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                PaidOrAfterPaidStatuses.Contains(order.Status) &&
                !order.Payments.Any(payment => payment.Status == PaymentStatus.Succeeded))
            .Select(order => new CheckoutConsistencyIssueResponse
            {
                IssueType = "PaidOrderWithoutSucceededPayment",
                Severity = "Critical",
                Message = "Paid or fulfilled order does not have a succeeded payment record.",
                RecommendedAction = "Investigate payment confirmation history before fulfillment continues.",
                DetectedAt = now,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderStatus = order.Status.ToString(),
                OrderTotal = order.Total
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CheckoutConsistencyIssueResponse>> GetSucceededPaymentsWithInvalidOrderStatusAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        return await dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.Status == PaymentStatus.Succeeded &&
                !PaidOrAfterPaidStatuses.Contains(payment.Order.Status))
            .Select(payment => new CheckoutConsistencyIssueResponse
            {
                IssueType = "SucceededPaymentWithInvalidOrderStatus",
                Severity = "Critical",
                Message = "Succeeded payment is attached to an order that is not paid or in a post-paid state.",
                RecommendedAction = "Investigate payment/order status transition and reconcile manually.",
                DetectedAt = now,
                OrderId = payment.OrderId,
                OrderNumber = payment.Order.OrderNumber,
                OrderStatus = payment.Order.Status.ToString(),
                OrderTotal = payment.Order.Total,
                PaymentId = payment.Id,
                PaymentStatus = payment.Status.ToString()
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CheckoutConsistencyIssueResponse>> GetProductsWithReservedQuantityMismatchAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var pendingReservationQuantities = dbContext.InventoryReservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.Status == InventoryReservationStatus.Pending &&
                reservation.ProductVariantId == null)
            .GroupBy(reservation => reservation.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                ExpectedReservedQuantity = group.Sum(reservation => reservation.Quantity)
            });

        return await dbContext.InventoryItems
            .AsNoTracking()
            .Include(inventoryItem => inventoryItem.Product)
            .GroupJoin(
                pendingReservationQuantities,
                inventoryItem => inventoryItem.ProductId,
                reservationQuantity => reservationQuantity.ProductId,
                (inventoryItem, reservationQuantities) => new
                {
                    inventoryItem.ProductId,
                    ProductName = inventoryItem.Product.Name,
                    ActualReservedQuantity = inventoryItem.QuantityReserved,
                    ExpectedReservedQuantity = reservationQuantities
                        .Select(reservationQuantity => reservationQuantity.ExpectedReservedQuantity)
                        .FirstOrDefault()
                })
            .Where(item => item.ActualReservedQuantity != item.ExpectedReservedQuantity)
            .Select(item => new CheckoutConsistencyIssueResponse
            {
                IssueType = "ReservedQuantityMismatch",
                Severity = "Critical",
                Message = "Inventory reserved quantity does not match the sum of pending reservations.",
                RecommendedAction = "Review inventory movements and reservations before manual correction.",
                DetectedAt = now,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ActualReservedQuantity = item.ActualReservedQuantity,
                ExpectedReservedQuantity = item.ExpectedReservedQuantity
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CheckoutConsistencyIssueResponse>> GetProductsWithNegativeInventoryAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        return await dbContext.InventoryItems
            .AsNoTracking()
            .Include(inventoryItem => inventoryItem.Product)
            .Where(inventoryItem =>
                inventoryItem.QuantityAvailable < 0 ||
                inventoryItem.QuantityReserved < 0)
            .Select(inventoryItem => new CheckoutConsistencyIssueResponse
            {
                IssueType = "NegativeInventoryQuantity",
                Severity = "Critical",
                Message = "Inventory item has a negative available or reserved quantity.",
                RecommendedAction = "Audit inventory movements and correct the stock quantity through inventory adjustment.",
                DetectedAt = now,
                ProductId = inventoryItem.ProductId,
                ProductName = inventoryItem.Product.Name,
                ActualReservedQuantity = inventoryItem.QuantityReserved
            })
            .ToListAsync(cancellationToken);
    }

    private static int GetSeverityRank(string severity)
    {
        return severity.Equals("Critical", StringComparison.OrdinalIgnoreCase)
            ? 2
            : severity.Equals("Warning", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
