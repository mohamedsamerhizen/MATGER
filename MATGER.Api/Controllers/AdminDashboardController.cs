using MATGER.Api.DTOs.Admin;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminDashboardController(
    IAdminReportingService adminReportingService) : ControllerBase
{
    private const int MaxReportRangeDays = 366;

    [HttpGet("stats")]
    public async Task<ActionResult<AdminDashboardStatsResponse>> GetStats(
        CancellationToken cancellationToken)
    {
        var response = await adminReportingService.GetStatsAsync(cancellationToken);

        return Ok(response);
    }

    [HttpGet("sales-report")]
    public async Task<ActionResult<AdminSalesReportResponse>> GetSalesReport(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = NormalizeDateRange(from, to);

        var validationError = ValidateDateRange(fromDate, toDate);

        if (validationError is not null)
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, validationError));
        }

        var response = await adminReportingService.GetSalesReportAsync(
            fromDate,
            toDate,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("revenue-chart")]
    public async Task<ActionResult<IReadOnlyList<AdminRevenueChartPointResponse>>> GetRevenueChart(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = NormalizeDateRange(from, to);

        var validationError = ValidateDateRange(fromDate, toDate);

        if (validationError is not null)
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, validationError));
        }

        var response = await adminReportingService.GetRevenueChartAsync(
            fromDate,
            toDate,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("top-products")]
    public async Task<ActionResult<PaginatedResponse<AdminTopProductResponse>>> GetTopProducts(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = NormalizeDateRange(from, to);

        var validationError = ValidateDateRange(fromDate, toDate);

        if (validationError is not null)
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, validationError));
        }

        var response = await adminReportingService.GetTopProductsAsync(
            fromDate,
            toDate,
            page,
            pageSize,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("order-status-breakdown")]
    public async Task<ActionResult<IReadOnlyList<AdminOrderStatusBreakdownResponse>>> GetOrderStatusBreakdown(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = NormalizeDateRange(from, to);

        var validationError = ValidateDateRange(fromDate, toDate);

        if (validationError is not null)
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, validationError));
        }

        var response = await adminReportingService.GetOrderStatusBreakdownAsync(
            fromDate,
            toDate,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("coupon-performance")]
    public async Task<ActionResult<PaginatedResponse<AdminCouponPerformanceResponse>>> GetCouponPerformance(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = NormalizeDateRange(from, to);

        var validationError = ValidateDateRange(fromDate, toDate);

        if (validationError is not null)
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, validationError));
        }

        var response = await adminReportingService.GetCouponPerformanceAsync(
            fromDate,
            toDate,
            page,
            pageSize,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("customer-insights")]
    public async Task<ActionResult<PaginatedResponse<AdminCustomerInsightResponse>>> GetCustomerInsights(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = NormalizeDateRange(from, to);

        var validationError = ValidateDateRange(fromDate, toDate);

        if (validationError is not null)
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, validationError));
        }

        var response = await adminReportingService.GetCustomerInsightsAsync(
            fromDate,
            toDate,
            page,
            pageSize,
            cancellationToken);

        return Ok(response);
    }

    private static (DateTime From, DateTime To) NormalizeDateRange(
        DateTime? from,
        DateTime? to)
    {
        var today = DateTime.UtcNow.Date;

        var toDate = (to ?? today).Date;
        var fromDate = (from ?? toDate.AddDays(-29)).Date;

        return (fromDate, toDate);
    }

    private static string? ValidateDateRange(
        DateTime from,
        DateTime to)
    {
        if (from > to)
        {
            return "The from date must be earlier than or equal to the to date.";
        }

        var rangeDays = (to - from).Days + 1;

        if (rangeDays > MaxReportRangeDays)
        {
            return $"The report date range cannot exceed {MaxReportRangeDays} days.";
        }

        return null;
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
}
