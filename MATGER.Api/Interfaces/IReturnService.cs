using MATGER.Api.DTOs.Returns;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Interfaces;

public interface IReturnService
{
    Task<ActionResult<ReturnRequestResponse>> CreateAsync(
        Guid orderId,
        CreateReturnRequest request,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ReturnRequestResponse>> ApproveAsync(
        Guid id,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ReturnRequestResponse>> RejectAsync(
        Guid id,
        RejectReturnRequest? request,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ReturnRequestResponse>> CompleteAsync(
        Guid id,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default);
}