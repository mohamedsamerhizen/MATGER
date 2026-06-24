using MATGER.Api.Data;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class RiskSignalService(ApplicationDbContext dbContext) : IRiskSignalService
{
    private const decimal HighValueOrderThreshold = 500000m;
    private const decimal NewAddressHighValueThreshold = 300000m;
    private const int SuspiciousLineQuantity = 5;

    public async Task EvaluateOrderAsync(
        Order order,
        CustomerAddress shippingAddress,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var signals = new List<RiskSignal>();

        var previousOrderCount = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(existingOrder => existingOrder.UserId == order.UserId, cancellationToken);

        if (previousOrderCount == 0 && order.Total >= HighValueOrderThreshold)
        {
            signals.Add(CreateSignal(
                order,
                "NewCustomerHighValueOrder",
                RiskSignalSeverity.High,
                $"New customer started checkout with total {order.Total:0.##}.",
                now));
        }

        var recentOrderCount = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(existingOrder =>
                existingOrder.UserId == order.UserId &&
                existingOrder.CreatedAt >= now.AddHours(-24),
                cancellationToken);

        if (recentOrderCount >= 3)
        {
            signals.Add(CreateSignal(
                order,
                "HighOrderFrequency24h",
                RiskSignalSeverity.Medium,
                $"Customer has {recentOrderCount} previous orders in the last 24 hours.",
                now));
        }

        var previousOrderIds = await dbContext.Orders
            .AsNoTracking()
            .Where(existingOrder => existingOrder.UserId == order.UserId)
            .Select(existingOrder => existingOrder.Id)
            .ToListAsync(cancellationToken);

        if (previousOrderIds.Count >= 3)
        {
            var returnedOrderIds = await dbContext.ReturnRequests
                .AsNoTracking()
                .Where(returnRequest => previousOrderIds.Contains(returnRequest.OrderId))
                .Select(returnRequest => returnRequest.OrderId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var refundedOrderIds = await dbContext.Refunds
                .AsNoTracking()
                .Where(refund => previousOrderIds.Contains(refund.OrderId))
                .Select(refund => refund.OrderId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var affectedOrders = returnedOrderIds
                .Concat(refundedOrderIds)
                .ToHashSet();
            var refundRatio = (decimal)affectedOrders.Count / previousOrderIds.Count;

            if (refundRatio >= 0.3m)
            {
                signals.Add(CreateSignal(
                    order,
                    "HighRefundRatio",
                    RiskSignalSeverity.High,
                    $"Customer return/refund ratio is {refundRatio:P0} across {previousOrderIds.Count} previous orders.",
                    now));
            }
        }

        if (order.Total >= NewAddressHighValueThreshold &&
            shippingAddress.CreatedAt >= now.AddDays(-7))
        {
            signals.Add(CreateSignal(
                order,
                "NewShippingAddressHighValue",
                RiskSignalSeverity.Medium,
                $"High-value checkout uses a shipping address created at {shippingAddress.CreatedAt:O}.",
                now));
        }

        var maxLineQuantity = order.Items.Count == 0
            ? 0
            : order.Items.Max(item => item.Quantity);

        if (maxLineQuantity >= SuspiciousLineQuantity)
        {
            signals.Add(CreateSignal(
                order,
                "SuspiciousQuantity",
                RiskSignalSeverity.Medium,
                $"Checkout includes a line quantity of {maxLineQuantity}.",
                now));
        }

        var recentFailedPayments = await dbContext.PaymentAttempts
            .AsNoTracking()
            .CountAsync(attempt =>
                attempt.Status == PaymentAttemptStatus.Failed &&
                attempt.CreatedAt >= now.AddHours(-24) &&
                attempt.Payment.Order.UserId == order.UserId,
                cancellationToken);

        if (recentFailedPayments >= 2)
        {
            signals.Add(CreateSignal(
                order,
                "RepeatedFailedPayments",
                RiskSignalSeverity.Medium,
                $"Customer has {recentFailedPayments} failed payment attempts in the last 24 hours.",
                now));
        }

        if (order.CouponId.HasValue)
        {
            var recentCouponRedemptions = await dbContext.CouponRedemptions
                .AsNoTracking()
                .CountAsync(redemption =>
                    redemption.UserId == order.UserId &&
                    redemption.CreatedAt >= now.AddDays(-7),
                    cancellationToken);

            if (recentCouponRedemptions >= 3)
            {
                signals.Add(CreateSignal(
                    order,
                    "CouponAbuse",
                    RiskSignalSeverity.Medium,
                    $"Customer has {recentCouponRedemptions} coupon redemptions in the last 7 days.",
                    now));
            }
        }

        if (signals.Count > 0)
        {
            dbContext.RiskSignals.AddRange(signals);
        }
    }

    private static RiskSignal CreateSignal(
        Order order,
        string signalType,
        RiskSignalSeverity severity,
        string details,
        DateTime createdAt)
    {
        return new RiskSignal
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            UserId = order.UserId,
            SignalType = signalType,
            Severity = severity,
            Details = details,
            CreatedAtUtc = createdAt,
            Status = RiskSignalStatus.Open
        };
    }
}
