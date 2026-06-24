using MATGER.Api.DTOs.Admin;
using MATGER.Api.DTOs.Common;

namespace MATGER.Api.Interfaces;

public interface IAdminReportingService
{
    Task<AdminDashboardStatsResponse> GetStatsAsync(
        CancellationToken cancellationToken = default);

    Task<AdminOperationsSummaryResponse> GetOperationsSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<AdminSalesOverviewResponse> GetSalesOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<AdminInventoryOverviewResponse> GetInventoryOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<AdminSalesReportResponse> GetSalesReportAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<AdminProfitReportResponse> GetProfitReportAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminRevenueChartPointResponse>> GetRevenueChartAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<AdminTopProductResponse>> GetTopProductsAsync(
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminOrderStatusBreakdownResponse>> GetOrderStatusBreakdownAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<AdminCouponPerformanceResponse>> GetCouponPerformanceAsync(
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<AdminCustomerInsightResponse>> GetCustomerInsightsAsync(
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
