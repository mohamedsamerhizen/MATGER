using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Shipping;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/shipping-methods")]
public sealed class ShippingMethodsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ShippingMethodResponse>>> GetActive(
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.ShippingMethods
            .AsNoTracking()
            .Where(method => method.IsActive);

        var totalCount = await query.CountAsync(cancellationToken);

        var methods = await query
            .OrderBy(method => method.BaseCost)
            .ThenBy(method => method.EstimatedDeliveryDays)
            .ThenBy(method => method.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(method => ToResponse(method))
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<ShippingMethodResponse>.Create(
            methods,
            page,
            pageSize,
            totalCount));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("admin")]
    public async Task<ActionResult<PaginatedResponse<ShippingMethodResponse>>> GetAllForAdmin(
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.ShippingMethods
            .AsNoTracking()
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(method => method.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var methods = await query
            .OrderBy(method => method.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(method => ToResponse(method))
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<ShippingMethodResponse>.Create(
            methods,
            page,
            pageSize,
            totalCount));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    public async Task<ActionResult<ShippingMethodResponse>> Create(
        CreateShippingMethodRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCreateRequest(request);

        if (validationError is not null)
        {
            return BadRequest(Error(StatusCodes.Status400BadRequest, validationError));
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var codeExists = await dbContext.ShippingMethods
            .AnyAsync(method => method.Code == normalizedCode, cancellationToken);

        if (codeExists)
        {
            return Conflict(Error(StatusCodes.Status409Conflict, "Shipping method code already exists."));
        }

        var now = DateTime.UtcNow;

        var method = new ShippingMethod
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Code = normalizedCode,
            BaseCost = request.BaseCost,
            EstimatedDeliveryDays = request.EstimatedDeliveryDays,
            IsActive = request.IsActive,
            CreatedAt = now
        };

        dbContext.ShippingMethods.Add(method);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ObjectResult(ToResponse(method))
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ShippingMethodResponse>> Update(
        Guid id,
        UpdateShippingMethodRequest request,
        CancellationToken cancellationToken)
    {
        var method = await dbContext.ShippingMethods
            .FirstOrDefaultAsync(method => method.Id == id, cancellationToken);

        if (method is null)
        {
            return NotFound(Error(StatusCodes.Status404NotFound, "Shipping method was not found."));
        }

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(Error(StatusCodes.Status400BadRequest, "Shipping method name is required."));
            }

            method.Name = request.Name.Trim();
        }

        if (request.Code is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(Error(StatusCodes.Status400BadRequest, "Shipping method code is required."));
            }

            var normalizedCode = request.Code.Trim().ToUpperInvariant();

            var codeExists = await dbContext.ShippingMethods
                .AnyAsync(other => other.Id != id && other.Code == normalizedCode, cancellationToken);

            if (codeExists)
            {
                return Conflict(Error(StatusCodes.Status409Conflict, "Shipping method code already exists."));
            }

            method.Code = normalizedCode;
        }

        if (request.BaseCost.HasValue)
        {
            method.BaseCost = request.BaseCost.Value;
        }

        if (request.EstimatedDeliveryDays.HasValue)
        {
            method.EstimatedDeliveryDays = request.EstimatedDeliveryDays.Value;
        }

        if (request.IsActive.HasValue)
        {
            method.IsActive = request.IsActive.Value;
        }

        method.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(method));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("/api/orders/{orderId:guid}/shipping")]
    public async Task<ActionResult<OrderShippingResponse>> UpdateOrderShipping(
        Guid orderId,
        UpdateOrderShippingRequest request,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (order is null)
        {
            return NotFound(Error(StatusCodes.Status404NotFound, "Order was not found."));
        }

        if (request.ShippingMethodId.HasValue)
        {
            var method = await dbContext.ShippingMethods
                .FirstOrDefaultAsync(method =>
                    method.Id == request.ShippingMethodId.Value &&
                    method.IsActive,
                    cancellationToken);

            if (method is null)
            {
                return BadRequest(Error(StatusCodes.Status400BadRequest, "Active shipping method was not found."));
            }

            order.ShippingMethodId = method.Id;
            order.ShippingMethod = method;
            order.ShippingMethodNameSnapshot = method.Name;
            order.ShippingMethodCodeSnapshot = method.Code;
            order.ShippingEstimatedDeliveryDays = method.EstimatedDeliveryDays;
            order.ShippingFee = method.BaseCost;
            order.Total = Math.Max(order.Subtotal - order.DiscountAmount + order.ShippingFee, 0m);
        }

        if (request.ShippingStatus.HasValue)
        {
            var validationError = ValidateShippingStatusUpdate(order, request.ShippingStatus.Value);

            if (validationError is not null)
            {
                return BadRequest(Error(StatusCodes.Status400BadRequest, validationError));
            }

            order.ShippingStatus = request.ShippingStatus.Value;

            if (request.ShippingStatus.Value == ShippingStatus.Shipped)
            {
                order.ShippedAt ??= DateTime.UtcNow;
            }

            if (request.ShippingStatus.Value == ShippingStatus.Delivered)
            {
                order.DeliveredAt ??= DateTime.UtcNow;
            }
        }

        if (request.CarrierName is not null)
        {
            order.ShippingCarrier = NormalizeOptional(request.CarrierName);
        }

        if (request.TrackingNumber is not null)
        {
            order.TrackingNumber = NormalizeOptional(request.TrackingNumber);
        }

        if (request.ShippingNote is not null)
        {
            order.DeliveryNote = NormalizeOptional(request.ShippingNote);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToOrderShippingResponse(order));
    }

    private static string? ValidateShippingStatusUpdate(Order order, ShippingStatus shippingStatus)
    {
        if (shippingStatus == ShippingStatus.Shipped && order.Status != OrderStatus.Shipped)
        {
            return "Order must be marked as shipped before shipping status can be set to shipped.";
        }

        if (shippingStatus == ShippingStatus.Delivered && order.Status != OrderStatus.Delivered)
        {
            return "Order must be marked as delivered before shipping status can be set to delivered.";
        }

        if ((order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Refunded) &&
            shippingStatus != order.ShippingStatus)
        {
            return "Shipping status cannot be changed for cancelled or refunded orders.";
        }

        return null;
    }

    private static string? ValidateCreateRequest(CreateShippingMethodRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Shipping method name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Shipping method code is required.";
        }

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    private static ShippingMethodResponse ToResponse(ShippingMethod method)
    {
        return new ShippingMethodResponse
        {
            Id = method.Id,
            Name = method.Name,
            Code = method.Code,
            BaseCost = method.BaseCost,
            EstimatedDeliveryDays = method.EstimatedDeliveryDays,
            IsActive = method.IsActive,
            CreatedAt = method.CreatedAt,
            UpdatedAt = method.UpdatedAt
        };
    }

    private static OrderShippingResponse ToOrderShippingResponse(Order order)
    {
        return new OrderShippingResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            ShippingMethodId = order.ShippingMethodId,
            ShippingMethodName = order.ShippingMethodNameSnapshot,
            ShippingMethodCode = order.ShippingMethodCodeSnapshot,
            ShippingFee = order.ShippingFee,
            EstimatedDeliveryDays = order.ShippingEstimatedDeliveryDays,
            ShippingStatus = order.ShippingStatus.ToString(),
            CarrierName = order.ShippingCarrier,
            TrackingNumber = order.TrackingNumber,
            ShippingNote = order.DeliveryNote,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt
        };
    }
}
