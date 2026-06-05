using MATGER.Api.DTOs.CheckoutConsistency;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/checkout-consistency")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class CheckoutConsistencyController(
    ICheckoutConsistencyService checkoutConsistencyService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<CheckoutConsistencySummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        var response = await checkoutConsistencyService.GetSummaryAsync(cancellationToken);

        return Ok(response);
    }

    [HttpGet("issues")]
    public async Task<ActionResult<PaginatedResponse<CheckoutConsistencyIssueResponse>>> GetIssues(
        [FromQuery] string? severity = null,
        [FromQuery] string? issueType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var response = await checkoutConsistencyService.GetIssuesAsync(
            severity,
            issueType,
            page,
            pageSize,
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("expire-pending-payments")]
    public async Task<ActionResult<CheckoutMaintenanceRunResponse>> ExpirePendingPayments(
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Invalid access token.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var response = await checkoutConsistencyService.ExpirePendingPaymentsAsync(
            userId.Value,
            cancellationToken);

        return Ok(response);
    }
}
