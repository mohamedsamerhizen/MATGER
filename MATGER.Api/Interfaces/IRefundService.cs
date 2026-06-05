using MATGER.Api.DTOs.Refunds;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Interfaces;

public interface IRefundService
{
    Task<ActionResult<RefundResponse>> CreateAsync(
        Guid orderId,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default);
}