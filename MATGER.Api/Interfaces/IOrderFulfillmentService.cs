using MATGER.Api.DTOs.Orders;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Interfaces;

public interface IOrderFulfillmentService
{
    Task<ActionResult<OrderStateChangedResponse>> CancelAsync(
        Guid orderId,
        CancelOrderRequest? request,
        Guid actorUserId,
        bool canSeeAllOrders,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<OrderStateChangedResponse>> MarkProcessingAsync(
        Guid orderId,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<OrderStateChangedResponse>> MarkShippedAsync(
        Guid orderId,
        ShipOrderRequest? request,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<OrderStateChangedResponse>> MarkDeliveredAsync(
        Guid orderId,
        DeliverOrderRequest? request,
        Guid actorUserId,
        string traceId,
        CancellationToken cancellationToken = default);
}