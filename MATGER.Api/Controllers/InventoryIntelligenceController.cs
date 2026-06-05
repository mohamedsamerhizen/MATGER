using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Inventory;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using MATGER.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/inventory/intelligence")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class InventoryIntelligenceController(
    IInventoryIntelligenceService inventoryIntelligenceService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<InventoryHealthSummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        var response = await inventoryIntelligenceService.GetHealthSummaryAsync(cancellationToken);

        return Ok(response);
    }

    [HttpGet("needs-attention")]
    public async Task<ActionResult<PaginatedResponse<InventoryAttentionItemResponse>>> GetNeedsAttention(
        [FromQuery] string? status = InventoryAttentionStatuses.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = NormalizeStatus(status);

        if (!InventoryAttentionStatuses.Allowed.Contains(normalizedStatus))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Inventory attention status is invalid."));
        }

        var response = await inventoryIntelligenceService.GetNeedsAttentionAsync(
            normalizedStatus,
            page,
            pageSize,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("top-reserved")]
    public async Task<ActionResult<PaginatedResponse<TopReservedProductResponse>>> GetTopReserved(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var response = await inventoryIntelligenceService.GetTopReservedAsync(
            page,
            pageSize,
            cancellationToken);

        return Ok(response);
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? InventoryAttentionStatuses.All
            : status.Trim().ToLowerInvariant();
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
