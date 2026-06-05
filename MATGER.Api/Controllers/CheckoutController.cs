using MATGER.Api.DTOs.Checkout;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/checkout")]
[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
public sealed class CheckoutController(
    ICheckoutService checkoutService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpPost("start")]
    public async Task<ActionResult<CheckoutStartResponse>> Start(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CheckoutStartRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await checkoutService.StartCheckoutAsync(
            request,
            idempotencyKey,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [HttpPost("confirm-payment")]
    public async Task<ActionResult<PaymentResultResponse>> ConfirmPayment(
        ConfirmPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await checkoutService.ConfirmPaymentAsync(
            request,
            idempotencyKey,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [HttpPost("fail-payment")]
    public async Task<ActionResult<PaymentResultResponse>> FailPayment(
        FailPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await checkoutService.FailPaymentAsync(
            request,
            idempotencyKey,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
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