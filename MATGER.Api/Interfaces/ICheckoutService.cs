using MATGER.Api.DTOs.Checkout;
using MATGER.Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace MATGER.Api.Interfaces;

public interface ICheckoutService
{
    Task<ActionResult<CheckoutStartResponse>> StartCheckoutAsync(
        CheckoutStartRequest? request,
        string? idempotencyKey,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<PaymentResultResponse>> ConfirmPaymentAsync(
        ConfirmPaymentRequest request,
        string? idempotencyKey,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<ActionResult<PaymentResultResponse>> FailPaymentAsync(
        FailPaymentRequest request,
        string? idempotencyKey,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default);

    string GenerateOrderNumber();

    void ApplyShippingSnapshot(Order order, CustomerAddress address);

    object ToShippingAuditSnapshot(Order order);
}