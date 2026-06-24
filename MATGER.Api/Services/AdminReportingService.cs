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

        var orderStatusCounts = await dbContext.Orders
            .AsNoTracking()
            .GroupBy(order => order.Status)
            .Select(group => new
            {
                Status = group.Key,
                OrdersCount = group.Count()
            })
            .ToDictionaryAsync(
                item => item.Status,
                item => item.OrdersCount,
                cancellationToken);

        var revenueSummary = await dbContext.Orders
            .AsNoTracking()
            .Where(order => RevenueStatuses.Contains(order.Status))
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalRevenue = group.Sum(order => order.Total),
                TodayRevenue = group.Sum(order =>
                    order.PaidAt.HasValue && order.PaidAt.Value >= today
                        ? order.Total
                        : 0m)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var totalRefundedAmount = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.Status == RefundStatus.Completed)
            .SumAsync(refund => refund.Amount, cancellationToken);

        var pendingReturnRequests = await dbContext.ReturnRequests
            .AsNoTracking()
            .CountAsync(
                returnRequest => returnRequest.Status == ReturnRequestStatus.Requested,
                cancellationToken);

        var lowStockProducts = await dbContext.InventoryItems
            .AsNoTracking()
            .CountAsync(
                item =>
                    item.Product.IsActive &&
                    item.Product.Category.IsActive &&
                    item.QuantityAvailable <= item.LowStockThreshold,
                cancellationToken);

        var activeCoupons = await dbContext.Coupons
            .AsNoTracking()
            .CountAsync(coupon => coupon.IsActive, cancellationToken);

        var customers = await userManager.GetUsersInRoleAsync(ApplicationRoles.Customer);

        return new AdminDashboardStatsResponse
        {
            TotalOrders = orderStatusCounts.Values.Sum(),
            PendingPaymentOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.PendingPayment),
            PaidOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.Paid),
            ProcessingOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.Processing),
            ShippedOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.Shipped),
            DeliveredOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.Delivered),
            CancelledOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.Cancelled),
            PaymentFailedOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.PaymentFailed),
            ReturnRequestedOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.ReturnRequested),
            ReturnedOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.Returned),
            RefundedOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.Refunded),
            TotalRevenue = revenueSummary?.TotalRevenue ?? 0m,
            TodayRevenue = revenueSummary?.TodayRevenue ?? 0m,
            TotalRefundedAmount = totalRefundedAmount,
            PendingReturnRequests = pendingReturnRequests,
            LowStockProducts = lowStockProducts,
            ActiveCustomers = customers.Count(customer => customer.IsActive),
            ActiveCoupons = activeCoupons
        };
    }

    public async Task<AdminOperationsSummaryResponse> GetOperationsSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var orderStatusCounts = await dbContext.Orders
            .AsNoTracking()
            .GroupBy(order => order.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        var lowStockCount = await dbContext.InventoryItems
            .AsNoTracking()
            .CountAsync(
                item =>
                    item.Product.IsActive &&
                    item.Product.Category.IsActive &&
                    item.QuantityAvailable <= item.LowStockThreshold,
                cancellationToken);

        var criticalStockCount = await dbContext.InventoryItems
            .AsNoTracking()
            .CountAsync(
                item =>
                    item.Product.IsActive &&
                    item.Product.Category.IsActive &&
                    item.QuantityAvailable <= 0,
                cancellationToken);

        var pendingReturns = await dbContext.ReturnRequests
            .AsNoTracking()
            .CountAsync(
                returnRequest => returnRequest.Status == ReturnRequestStatus.Requested,
                cancellationToken);

        var pendingRefunds = await dbContext.Refunds
            .AsNoTracking()
            .CountAsync(
                refund => refund.Status == RefundStatus.Pending,
                cancellationToken);

        var openRiskSignals = await dbContext.RiskSignals
            .AsNoTracking()
            .CountAsync(
                signal => signal.Status == RiskSignalStatus.Open,
                cancellationToken);

        var pendingStockAdjustmentRequests = await dbContext.StockAdjustmentRequests
            .AsNoTracking()
            .CountAsync(
                request => request.Status == StockAdjustmentRequestStatus.Pending,
                cancellationToken);

        return new AdminOperationsSummaryResponse
        {
            PendingOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.PendingPayment),
            PaidAwaitingProcessingOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.Paid),
            ProcessingOrders = GetOrderStatusCount(orderStatusCounts, OrderStatus.Processing),
            LowStockCount = lowStockCount,
            CriticalStockCount = criticalStockCount,
            PendingReturns = pendingReturns,
            PendingRefunds = pendingRefunds,
            OpenRiskSignals = openRiskSignals,
            PendingStockAdjustmentRequests = pendingStockAdjustmentRequests,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<AdminSalesOverviewResponse> GetSalesOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var last7Days = today.AddDays(-6);
        var last30Days = today.AddDays(-29);

        var revenueOrders = dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                RevenueStatuses.Contains(order.Status) &&
                order.PaidAt.HasValue &&
                order.PaidAt.Value >= last30Days &&
                order.PaidAt.Value < tomorrow);

        var salesToday = await revenueOrders
            .Where(order => order.PaidAt!.Value >= today)
            .SumAsync(order => order.Total, cancellationToken);

        var salesLast7Days = await revenueOrders
            .Where(order => order.PaidAt!.Value >= last7Days)
            .SumAsync(order => order.Total, cancellationToken);

        var salesLast30Days = await revenueOrders
            .SumAsync(order => order.Total, cancellationToken);

        var ordersToday = await revenueOrders
            .CountAsync(order => order.PaidAt!.Value >= today, cancellationToken);

        var ordersLast7Days = await revenueOrders
            .CountAsync(order => order.PaidAt!.Value >= last7Days, cancellationToken);

        var ordersLast30Days = await revenueOrders
            .CountAsync(cancellationToken);

        var refundAmountLast30Days = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund =>
                refund.Status == RefundStatus.Completed &&
                (refund.CompletedAt ?? refund.CreatedAt) >= last30Days &&
                (refund.CompletedAt ?? refund.CreatedAt) < tomorrow)
            .SumAsync(refund => refund.Amount, cancellationToken);

        return new AdminSalesOverviewResponse
        {
            SalesToday = salesToday,
            SalesLast7Days = salesLast7Days,
            SalesLast30Days = salesLast30Days,
            OrdersToday = ordersToday,
            OrdersLast7Days = ordersLast7Days,
            OrdersLast30Days = ordersLast30Days,
            AverageOrderValueLast30Days = ordersLast30Days == 0
                ? 0m
                : Math.Round(salesLast30Days / ordersLast30Days, 2),
            RefundAmountLast30Days = refundAmountLast30Days,
            RefundRateLast30Days = salesLast30Days == 0m
                ? 0m
                : Math.Round(refundAmountLast30Days / salesLast30Days * 100m, 2),
            GeneratedAtUtc = now
        };
    }

    public async Task<AdminInventoryOverviewResponse> GetInventoryOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var deadStockCutoff = DateTime.UtcNow.Date.AddDays(-90);

        var soldProductIds = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                RevenueStatuses.Contains(item.Order.Status) &&
                item.Order.PaidAt.HasValue &&
                item.Order.PaidAt.Value >= deadStockCutoff)
            .Select(item => item.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var soldProductIdsSet = soldProductIds.ToHashSet();

        var inventory = await dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => item.Product.IsActive && item.Product.Category.IsActive)
            .Select(item => new
            {
                item.Id,
                item.ProductId,
                ProductName = item.Product.Name,
                ProductSku = item.Product.SKU,
                item.QuantityAvailable,
                item.QuantityReserved,
                item.LowStockThreshold,
                ProductCostPrice = item.Product.CostPrice ?? 0m,
                ProductPrice = item.Product.Price
            })
            .ToListAsync(cancellationToken);

        var topReservedItems = inventory
            .Where(item => item.QuantityReserved > 0)
            .OrderByDescending(item => item.QuantityReserved)
            .ThenBy(item => item.QuantityAvailable)
            .ThenBy(item => item.ProductName)
            .Take(10)
            .Select(item =>
            {
                var quantityOnHand = item.QuantityAvailable + item.QuantityReserved;
                var reservedSharePercentage = quantityOnHand == 0
                    ? 0m
                    : Math.Round((decimal)item.QuantityReserved / quantityOnHand * 100m, 2);

                return new AdminTopReservedInventoryItemResponse
                {
                    InventoryItemId = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    ProductSku = item.ProductSku,
                    QuantityAvailable = item.QuantityAvailable,
                    QuantityReserved = item.QuantityReserved,
                    QuantityOnHand = quantityOnHand,
                    ReservedSharePercentage = reservedSharePercentage
                };
            })
            .ToList();

        return new AdminInventoryOverviewResponse
        {
            TotalInventoryItems = inventory.Count,
            LowStockCount = inventory.Count(item => item.QuantityAvailable <= item.LowStockThreshold),
            CriticalStockCount = inventory.Count(item => item.QuantityAvailable <= 0),
            DeadStockCount = inventory.Count(item =>
                item.QuantityAvailable > 0 && !soldProductIdsSet.Contains(item.ProductId)),
            ReservedInventoryItems = inventory.Count(item => item.QuantityReserved > 0),
            TotalQuantityAvailable = inventory.Sum(item => item.QuantityAvailable),
            TotalQuantityReserved = inventory.Sum(item => item.QuantityReserved),
            TotalQuantityOnHand = inventory.Sum(item => item.QuantityAvailable + item.QuantityReserved),
            EstimatedCostValue = inventory.Sum(item =>
                (item.QuantityAvailable + item.QuantityReserved) * item.ProductCostPrice),
            EstimatedRetailValue = inventory.Sum(item =>
                (item.QuantityAvailable + item.QuantityReserved) * item.ProductPrice),
            TopReservedItems = topReservedItems,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static int GetOrderStatusCount(
        IReadOnlyDictionary<OrderStatus, int> orderStatusCounts,
        OrderStatus status)
    {
        return orderStatusCounts.TryGetValue(status, out var count)
            ? count
            : 0;
    }

    private static decimal CalculateMarginPercentage(decimal revenue, decimal grossProfit)
    {
        return revenue == 0m
            ? 0m
            : Math.Round(grossProfit / revenue * 100m, 2);
    }

    private static AdminLowMarginProductResponse ToLowMarginProduct(AdminProfitByProductResponse product)
    {
        return new AdminLowMarginProductResponse
        {
            ProductId = product.ProductId,
            ProductName = product.ProductName,
            ProductSku = product.ProductSku,
            Revenue = product.Revenue,
            Cost = product.Cost,
            GrossProfit = product.GrossProfit,
            GrossMarginPercentage = product.GrossMarginPercentage
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

    public async Task<AdminProfitReportResponse> GetProfitReportAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var fromInclusive = from.Date;
        var toExclusive = to.Date.AddDays(1);

        var orderItems = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                RevenueStatuses.Contains(item.Order.Status) &&
                item.Order.PaidAt.HasValue &&
                item.Order.PaidAt.Value >= fromInclusive &&
                item.Order.PaidAt.Value < toExclusive)
            .Select(item => new
            {
                item.ProductId,
                ProductName = item.ProductNameSnapshot,
                ProductSku = item.ProductSkuSnapshot,
                item.Product.CategoryId,
                CategoryName = item.Product.Category.Name,
                item.Quantity,
                Revenue = item.Total,
                Cost = (item.CostPriceSnapshot ?? 0m) * item.Quantity
            })
            .ToListAsync(cancellationToken);

        var revenue = orderItems.Sum(item => item.Revenue);
        var cost = orderItems.Sum(item => item.Cost);
        var grossProfit = revenue - cost;

        var profitByProduct = orderItems
            .GroupBy(item => new
            {
                item.ProductId,
                item.ProductName,
                item.ProductSku
            })
            .Select(group =>
            {
                var productRevenue = group.Sum(item => item.Revenue);
                var productCost = group.Sum(item => item.Cost);
                var productGrossProfit = productRevenue - productCost;

                return new AdminProfitByProductResponse
                {
                    ProductId = group.Key.ProductId,
                    ProductName = group.Key.ProductName,
                    ProductSku = group.Key.ProductSku,
                    QuantitySold = group.Sum(item => item.Quantity),
                    Revenue = productRevenue,
                    Cost = productCost,
                    GrossProfit = productGrossProfit,
                    GrossMarginPercentage = CalculateMarginPercentage(productRevenue, productGrossProfit)
                };
            })
            .OrderByDescending(item => item.GrossProfit)
            .ThenByDescending(item => item.Revenue)
            .ThenBy(item => item.ProductName)
            .Take(25)
            .ToList();

        var profitByCategory = orderItems
            .GroupBy(item => new
            {
                item.CategoryId,
                item.CategoryName
            })
            .Select(group =>
            {
                var categoryRevenue = group.Sum(item => item.Revenue);
                var categoryCost = group.Sum(item => item.Cost);
                var categoryGrossProfit = categoryRevenue - categoryCost;

                return new AdminProfitByCategoryResponse
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    QuantitySold = group.Sum(item => item.Quantity),
                    Revenue = categoryRevenue,
                    Cost = categoryCost,
                    GrossProfit = categoryGrossProfit,
                    GrossMarginPercentage = CalculateMarginPercentage(categoryRevenue, categoryGrossProfit)
                };
            })
            .OrderByDescending(item => item.GrossProfit)
            .ThenBy(item => item.CategoryName)
            .ToList();

        var lowMarginProducts = profitByProduct
            .Where(item => item.GrossProfit >= 0m && item.GrossMarginPercentage < 10m)
            .OrderBy(item => item.GrossMarginPercentage)
            .ThenBy(item => item.ProductName)
            .Select(ToLowMarginProduct)
            .ToList();

        var negativeMarginProducts = profitByProduct
            .Where(item => item.GrossProfit < 0m)
            .OrderBy(item => item.GrossMarginPercentage)
            .ThenBy(item => item.ProductName)
            .Select(ToLowMarginProduct)
            .ToList();

        return new AdminProfitReportResponse
        {
            From = fromInclusive,
            To = to.Date,
            GeneratedAt = DateTime.UtcNow,
            Revenue = revenue,
            Cost = cost,
            GrossProfit = grossProfit,
            GrossMarginPercentage = CalculateMarginPercentage(revenue, grossProfit),
            ProfitByProduct = profitByProduct,
            ProfitByCategory = profitByCategory,
            LowMarginProducts = lowMarginProducts,
            NegativeMarginProducts = negativeMarginProducts
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
