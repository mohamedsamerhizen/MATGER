using MATGER.Api.Data;
using MATGER.Api.DTOs.Admin;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Enums;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class AdminReportingService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : IAdminReportingService
{
    private static readonly OrderStatus[] RevenueStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Processing,
        OrderStatus.Shipped,
        OrderStatus.Delivered
    ];

    public async Task<AdminDashboardStatsResponse> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var orders = dbContext.Orders.AsNoTracking();

        var customers = await userManager.GetUsersInRoleAsync(ApplicationRoles.Customer);

        return new AdminDashboardStatsResponse
        {
            TotalOrders = await orders.CountAsync(cancellationToken),

            PendingPaymentOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.PendingPayment,
                cancellationToken),

            PaidOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.Paid,
                cancellationToken),

            ProcessingOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.Processing,
                cancellationToken),

            ShippedOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.Shipped,
                cancellationToken),

            DeliveredOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.Delivered,
                cancellationToken),

            CancelledOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.Cancelled,
                cancellationToken),

            PaymentFailedOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.PaymentFailed,
                cancellationToken),

            ReturnRequestedOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.ReturnRequested,
                cancellationToken),

            ReturnedOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.Returned,
                cancellationToken),

            RefundedOrders = await orders.CountAsync(
                order => order.Status == OrderStatus.Refunded,
                cancellationToken),

            TotalRevenue = await orders
                .Where(order => RevenueStatuses.Contains(order.Status))
                .SumAsync(order => order.Total, cancellationToken),

            TodayRevenue = await orders
                .Where(order =>
                    RevenueStatuses.Contains(order.Status) &&
                    order.PaidAt.HasValue &&
                    order.PaidAt.Value >= today)
                .SumAsync(order => order.Total, cancellationToken),

            TotalRefundedAmount = await dbContext.Refunds
                .AsNoTracking()
                .Where(refund => refund.Status == RefundStatus.Completed)
                .SumAsync(refund => refund.Amount, cancellationToken),

            PendingReturnRequests = await dbContext.ReturnRequests
                .AsNoTracking()
                .CountAsync(
                    returnRequest => returnRequest.Status == ReturnRequestStatus.Requested,
                    cancellationToken),

            LowStockProducts = await dbContext.InventoryItems
                .AsNoTracking()
                .CountAsync(
                    item =>
                        item.Product.IsActive &&
                        item.Product.Category.IsActive &&
                        item.QuantityAvailable <= item.LowStockThreshold,
                    cancellationToken),

            ActiveCustomers = customers.Count(customer => customer.IsActive),

            ActiveCoupons = await dbContext.Coupons
                .AsNoTracking()
                .CountAsync(coupon => coupon.IsActive, cancellationToken)
        };
    }

    public async Task<AdminSalesReportResponse> GetSalesReportAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var fromInclusive = from.Date;
        var toExclusive = to.Date.AddDays(1);

        var createdOrders = dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.CreatedAt >= fromInclusive &&
                order.CreatedAt < toExclusive);

        var revenueOrders = dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                RevenueStatuses.Contains(order.Status) &&
                order.PaidAt.HasValue &&
                order.PaidAt.Value >= fromInclusive &&
                order.PaidAt.Value < toExclusive);

        var completedRefunds = dbContext.Refunds
            .AsNoTracking()
            .Where(refund =>
                refund.Status == RefundStatus.Completed &&
                (refund.CompletedAt ?? refund.CreatedAt) >= fromInclusive &&
                (refund.CompletedAt ?? refund.CreatedAt) < toExclusive);

        var revenueOrdersCount = await revenueOrders.CountAsync(cancellationToken);
        var grossRevenue = await revenueOrders.SumAsync(order => order.Total, cancellationToken);
        var refundedAmount = await completedRefunds.SumAsync(refund => refund.Amount, cancellationToken);

        var itemsSold = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                RevenueStatuses.Contains(item.Order.Status) &&
                item.Order.PaidAt.HasValue &&
                item.Order.PaidAt.Value >= fromInclusive &&
                item.Order.PaidAt.Value < toExclusive)
            .SumAsync(item => item.Quantity, cancellationToken);

        return new AdminSalesReportResponse
        {
            From = fromInclusive,
            To = to.Date,
            GeneratedAt = DateTime.UtcNow,

            TotalOrdersCreated = await createdOrders.CountAsync(cancellationToken),

            PendingPaymentOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.PendingPayment,
                cancellationToken),

            PaymentFailedOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.PaymentFailed,
                cancellationToken),

            PaidOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.Paid,
                cancellationToken),

            ProcessingOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.Processing,
                cancellationToken),

            ShippedOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.Shipped,
                cancellationToken),

            DeliveredOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.Delivered,
                cancellationToken),

            CancelledOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.Cancelled,
                cancellationToken),

            ReturnRequestedOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.ReturnRequested,
                cancellationToken),

            ReturnedOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.Returned,
                cancellationToken),

            RefundedOrders = await createdOrders.CountAsync(
                order => order.Status == OrderStatus.Refunded,
                cancellationToken),

            RevenueOrdersCount = revenueOrdersCount,
            UniqueCustomersCount = await revenueOrders
                .Select(order => order.UserId)
                .Distinct()
                .CountAsync(cancellationToken),
            ItemsSold = itemsSold,
            GrossRevenue = grossRevenue,
            TotalDiscountAmount = await revenueOrders.SumAsync(order => order.DiscountAmount, cancellationToken),
            TotalShippingFees = await revenueOrders.SumAsync(order => order.ShippingFee, cancellationToken),
            RefundedAmount = refundedAmount,
            NetRevenue = grossRevenue - refundedAmount,
            AverageOrderValue = revenueOrdersCount == 0
                ? 0m
                : Math.Round(grossRevenue / revenueOrdersCount, 2)
        };
    }

    public async Task<IReadOnlyList<AdminRevenueChartPointResponse>> GetRevenueChartAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var fromInclusive = from.Date;
        var toExclusive = to.Date.AddDays(1);

        var revenueByDate = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                RevenueStatuses.Contains(order.Status) &&
                order.PaidAt.HasValue &&
                order.PaidAt.Value >= fromInclusive &&
                order.PaidAt.Value < toExclusive)
            .GroupBy(order => order.PaidAt!.Value.Date)
            .Select(group => new
            {
                Date = group.Key,
                OrdersCount = group.Count(),
                GrossRevenue = group.Sum(order => order.Total)
            })
            .ToDictionaryAsync(item => item.Date, cancellationToken);

        var itemsSoldByDate = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                RevenueStatuses.Contains(item.Order.Status) &&
                item.Order.PaidAt.HasValue &&
                item.Order.PaidAt.Value >= fromInclusive &&
                item.Order.PaidAt.Value < toExclusive)
            .GroupBy(item => item.Order.PaidAt!.Value.Date)
            .Select(group => new
            {
                Date = group.Key,
                ItemsSold = group.Sum(item => item.Quantity)
            })
            .ToDictionaryAsync(item => item.Date, item => item.ItemsSold, cancellationToken);

        var refundsByDate = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund =>
                refund.Status == RefundStatus.Completed &&
                (refund.CompletedAt ?? refund.CreatedAt) >= fromInclusive &&
                (refund.CompletedAt ?? refund.CreatedAt) < toExclusive)
            .GroupBy(refund => (refund.CompletedAt ?? refund.CreatedAt).Date)
            .Select(group => new
            {
                Date = group.Key,
                RefundedAmount = group.Sum(refund => refund.Amount)
            })
            .ToDictionaryAsync(item => item.Date, item => item.RefundedAmount, cancellationToken);

        var daysCount = (to.Date - fromInclusive).Days + 1;

        return Enumerable
            .Range(0, daysCount)
            .Select(index => fromInclusive.AddDays(index))
            .Select(date =>
            {
                revenueByDate.TryGetValue(date, out var revenue);
                itemsSoldByDate.TryGetValue(date, out var itemsSold);
                refundsByDate.TryGetValue(date, out var refundedAmount);

                var grossRevenue = revenue?.GrossRevenue ?? 0m;

                return new AdminRevenueChartPointResponse
                {
                    Date = date,
                    OrdersCount = revenue?.OrdersCount ?? 0,
                    ItemsSold = itemsSold,
                    GrossRevenue = grossRevenue,
                    RefundedAmount = refundedAmount,
                    NetRevenue = grossRevenue - refundedAmount
                };
            })
            .ToList();
    }

    public async Task<PaginatedResponse<AdminTopProductResponse>> GetTopProductsAsync(
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var fromInclusive = from.Date;
        var toExclusive = to.Date.AddDays(1);

        var query = dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                RevenueStatuses.Contains(item.Order.Status) &&
                item.Order.PaidAt.HasValue &&
                item.Order.PaidAt.Value >= fromInclusive &&
                item.Order.PaidAt.Value < toExclusive)
            .GroupBy(item => new
            {
                item.ProductId,
                ProductName = item.Product.Name,
                item.ProductNameSnapshot,
                ProductSku = item.Product.SKU
            })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ProductName,
                group.Key.ProductNameSnapshot,
                group.Key.ProductSku,
                OrdersCount = group.Select(item => item.OrderId).Distinct().Count(),
                QuantitySold = group.Sum(item => item.Quantity),
                GrossRevenue = group.Sum(item => item.Total)
            });

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(item => item.GrossRevenue)
            .ThenByDescending(item => item.QuantitySold)
            .ThenBy(item => item.ProductName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(item => new AdminTopProductResponse
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ProductNameSnapshot = item.ProductNameSnapshot,
                ProductSku = item.ProductSku,
                OrdersCount = item.OrdersCount,
                QuantitySold = item.QuantitySold,
                GrossRevenue = item.GrossRevenue,
                AverageUnitPrice = item.QuantitySold == 0
                    ? 0m
                    : Math.Round(item.GrossRevenue / item.QuantitySold, 2)
            })
            .ToList();

        return PaginatedResponse<AdminTopProductResponse>.Create(
            items,
            page,
            pageSize,
            totalCount);
    }

    public async Task<IReadOnlyList<AdminOrderStatusBreakdownResponse>> GetOrderStatusBreakdownAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var fromInclusive = from.Date;
        var toExclusive = to.Date.AddDays(1);

        var totalOrders = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(
                order =>
                    order.CreatedAt >= fromInclusive &&
                    order.CreatedAt < toExclusive,
                cancellationToken);

        var rawItems = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.CreatedAt >= fromInclusive &&
                order.CreatedAt < toExclusive)
            .GroupBy(order => order.Status)
            .Select(group => new
            {
                Status = group.Key,
                OrdersCount = group.Count(),
                TotalAmount = group.Sum(order => order.Total)
            })
            .ToListAsync(cancellationToken);

        var byStatus = rawItems.ToDictionary(item => item.Status);

        return Enum.GetValues<OrderStatus>()
            .OrderBy(status => (int)status)
            .Select(status =>
            {
                byStatus.TryGetValue(status, out var item);

                var ordersCount = item?.OrdersCount ?? 0;

                return new AdminOrderStatusBreakdownResponse
                {
                    Status = status,
                    StatusName = status.ToString(),
                    OrdersCount = ordersCount,
                    Percentage = totalOrders == 0
                        ? 0m
                        : Math.Round((decimal)ordersCount / totalOrders * 100m, 2),
                    TotalAmount = item?.TotalAmount ?? 0m
                };
            })
            .ToList();
    }

    public async Task<PaginatedResponse<AdminCouponPerformanceResponse>> GetCouponPerformanceAsync(
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var fromInclusive = from.Date;
        var toExclusive = to.Date.AddDays(1);

        var query = dbContext.CouponRedemptions
            .AsNoTracking()
            .Where(redemption =>
                redemption.CreatedAt >= fromInclusive &&
                redemption.CreatedAt < toExclusive)
            .GroupBy(redemption => new
            {
                redemption.CouponId,
                CouponCode = redemption.CodeSnapshot,
                CouponName = redemption.Coupon.Name
            })
            .Select(group => new
            {
                group.Key.CouponId,
                group.Key.CouponCode,
                group.Key.CouponName,
                RedemptionsCount = group.Count(),
                UniqueCustomersCount = group.Select(redemption => redemption.UserId).Distinct().Count(),
                OrdersCount = group.Select(redemption => redemption.OrderId).Distinct().Count(),
                TotalDiscountAmount = group.Sum(redemption => redemption.DiscountAmount),
                RevenueAfterDiscount = group.Sum(redemption => redemption.Order.Total)
            });

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(item => item.TotalDiscountAmount)
            .ThenByDescending(item => item.RevenueAfterDiscount)
            .ThenBy(item => item.CouponCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(item => new AdminCouponPerformanceResponse
            {
                CouponId = item.CouponId,
                CouponCode = item.CouponCode,
                CouponName = item.CouponName,
                RedemptionsCount = item.RedemptionsCount,
                UniqueCustomersCount = item.UniqueCustomersCount,
                OrdersCount = item.OrdersCount,
                TotalDiscountAmount = item.TotalDiscountAmount,
                RevenueAfterDiscount = item.RevenueAfterDiscount,
                AverageDiscountAmount = item.RedemptionsCount == 0
                    ? 0m
                    : Math.Round(item.TotalDiscountAmount / item.RedemptionsCount, 2),
                DiscountToRevenuePercentage = item.RevenueAfterDiscount == 0
                    ? 0m
                    : Math.Round(item.TotalDiscountAmount / item.RevenueAfterDiscount * 100m, 2)
            })
            .ToList();

        return PaginatedResponse<AdminCouponPerformanceResponse>.Create(
            items,
            page,
            pageSize,
            totalCount);
    }

    public async Task<PaginatedResponse<AdminCustomerInsightResponse>> GetCustomerInsightsAsync(
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var fromInclusive = from.Date;
        var toExclusive = to.Date.AddDays(1);

        var query = dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                RevenueStatuses.Contains(order.Status) &&
                order.PaidAt.HasValue &&
                order.PaidAt.Value >= fromInclusive &&
                order.PaidAt.Value < toExclusive)
            .GroupBy(order => new
            {
                order.UserId,
                order.User.FullName,
                Email = order.User.Email!
            })
            .Select(group => new
            {
                CustomerId = group.Key.UserId,
                group.Key.FullName,
                group.Key.Email,
                OrdersCount = group.Count(),
                ItemsPurchased = group.SelectMany(order => order.Items).Sum(item => item.Quantity),
                TotalSpent = group.Sum(order => order.Total),
                FirstPaidOrderAt = group.Min(order => order.PaidAt!.Value),
                LastPaidOrderAt = group.Max(order => order.PaidAt!.Value)
            });

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(item => item.TotalSpent)
            .ThenByDescending(item => item.OrdersCount)
            .ThenBy(item => item.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(item => new AdminCustomerInsightResponse
            {
                CustomerId = item.CustomerId,
                FullName = item.FullName,
                Email = item.Email,
                OrdersCount = item.OrdersCount,
                ItemsPurchased = item.ItemsPurchased,
                TotalSpent = item.TotalSpent,
                AverageOrderValue = item.OrdersCount == 0
                    ? 0m
                    : Math.Round(item.TotalSpent / item.OrdersCount, 2),
                FirstPaidOrderAt = item.FirstPaidOrderAt,
                LastPaidOrderAt = item.LastPaidOrderAt
            })
            .ToList();

        return PaginatedResponse<AdminCustomerInsightResponse>.Create(
            items,
            page,
            pageSize,
            totalCount);
    }
}

