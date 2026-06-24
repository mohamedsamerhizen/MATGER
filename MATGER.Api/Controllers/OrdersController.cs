using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Orders;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Helpers;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController(
    ApplicationDbContext dbContext,
    IOrderFulfillmentService orderFulfillmentService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<OrderResponse>>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? paymentStatus = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? customerEmail = null,
        [FromQuery] string? orderNumber = null,
        [FromQuery] string? couponCode = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = PaginationHelper.DefaultPage,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = currentUserService.UserId;

        if (currentUserId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var canSeeAllOrders = CanSeeAllOrders();
        (page, pageSize) = PaginationHelper.Normalize(page, pageSize);

        var query = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.User)
            .Include(order => order.Coupon)
            .Include(order => order.Items)
            .ThenInclude(item => item.ProductVariant)
            .Include(order => order.InventoryReservations)
            .ThenInclude(reservation => reservation.Product)
            .Include(order => order.InventoryReservations)
            .ThenInclude(reservation => reservation.ProductVariant)
            .AsQueryable();

        if (!canSeeAllOrders)
        {
            query = query.Where(order => order.UserId == currentUserId.Value);
        }
        else if (userId.HasValue)
        {
            query = query.Where(order => order.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Order status is invalid."));
            }

            query = query.Where(order => order.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            if (!Enum.TryParse<PaymentStatus>(paymentStatus.Trim(), ignoreCase: true, out var parsedPaymentStatus))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Payment status is invalid."));
            }

            query = query.Where(order => order.Payments.Any(payment => payment.Status == parsedPaymentStatus));
        }

        if (canSeeAllOrders && !string.IsNullOrWhiteSpace(customerEmail))
        {
            var normalizedCustomerEmail = customerEmail.Trim();

            query = query.Where(order =>
                order.User.Email != null &&
                order.User.Email.Contains(normalizedCustomerEmail));
        }

        if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            var normalizedOrderNumber = orderNumber.Trim();

            query = query.Where(order => order.OrderNumber.Contains(normalizedOrderNumber));
        }

        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var normalizedCouponCode = couponCode.Trim().ToUpperInvariant();

            query = query.Where(order =>
                order.Coupon != null &&
                order.Coupon.Code == normalizedCouponCode);
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "From date cannot be later than to date."));
        }

        if (from.HasValue)
        {
            query = query.Where(order => order.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(order => order.CreatedAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .OrderByDescending(order => order.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => ToResponse(order))
            .ToListAsync(cancellationToken);

        return Ok(PaginatedResponse<OrderResponse>.Create(
            orders,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Coupon)
            .Include(order => order.Items)
            .ThenInclude(item => item.ProductVariant)
            .Include(order => order.InventoryReservations)
            .ThenInclude(reservation => reservation.Product)
            .Include(order => order.InventoryReservations)
            .ThenInclude(reservation => reservation.ProductVariant)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        if (!CanSeeAllOrders() && order.UserId != userId.Value)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        return Ok(ToResponse(order));
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyList<OrderTimelineEventResponse>>> GetTimeline(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.InventoryReservations)
            .Include(order => order.ReturnRequests)
            .Include(order => order.Refunds)
            .Include(order => order.StatusHistories)
            .ThenInclude(history => history.ChangedByUser)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        if (!CanSeeAllOrders() && order.UserId != userId.Value)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        var timeline = new List<OrderTimelineEventResponse>();
        var hasRecordedStatusHistory = order.StatusHistories.Count > 0;

        if (hasRecordedStatusHistory)
        {
            timeline.AddRange(order.StatusHistories.Select(history => new OrderTimelineEventResponse
            {
                Event = history.PreviousStatus is null
                    ? "OrderCreated"
                    : "OrderStatusChanged",
                OccurredAt = history.CreatedAt,
                Description = history.PreviousStatus is null
                    ? $"Order {order.OrderNumber} was created with status {history.NewStatus}."
                    : $"Order status changed from {history.PreviousStatus} to {history.NewStatus}. Reason: {history.Reason}"
            }));
        }
        else
        {
            timeline.Add(new OrderTimelineEventResponse
            {
                Event = "OrderCreated",
                OccurredAt = order.CreatedAt,
                Description = $"Order {order.OrderNumber} was created with status {order.Status}."
            });

            AddLegacyStatusTimelineEvents(order, timeline);
        }

        foreach (var reservation in order.InventoryReservations)
        {
            timeline.Add(new OrderTimelineEventResponse
            {
                Event = "InventoryReserved",
                OccurredAt = reservation.CreatedAt,
                Description = $"Reserved {reservation.Quantity} item(s). Reservation status: {reservation.Status}."
            });

            if (reservation.ConfirmedAt.HasValue)
            {
                timeline.Add(new OrderTimelineEventResponse
                {
                    Event = "ReservationConfirmed",
                    OccurredAt = reservation.ConfirmedAt.Value,
                    Description = "Inventory reservation was confirmed."
                });
            }

            if (reservation.ReleasedAt.HasValue)
            {
                timeline.Add(new OrderTimelineEventResponse
                {
                    Event = "ReservationReleased",
                    OccurredAt = reservation.ReleasedAt.Value,
                    Description = "Inventory reservation was released."
                });
            }

            if (reservation.ExpiredAt.HasValue)
            {
                timeline.Add(new OrderTimelineEventResponse
                {
                    Event = "ReservationExpired",
                    OccurredAt = reservation.ExpiredAt.Value,
                    Description = "Inventory reservation expired."
                });
            }
        }

        foreach (var returnRequest in order.ReturnRequests)
        {
            timeline.Add(new OrderTimelineEventResponse
            {
                Event = "ReturnRequested",
                OccurredAt = returnRequest.RequestedAt,
                Description = "Customer requested a return."
            });

            if (returnRequest.ApprovedAt.HasValue)
            {
                timeline.Add(new OrderTimelineEventResponse
                {
                    Event = "ReturnApproved",
                    OccurredAt = returnRequest.ApprovedAt.Value,
                    Description = "Return request was approved."
                });
            }

            if (returnRequest.RejectedAt.HasValue)
            {
                timeline.Add(new OrderTimelineEventResponse
                {
                    Event = "ReturnRejected",
                    OccurredAt = returnRequest.RejectedAt.Value,
                    Description = "Return request was rejected."
                });
            }

            if (returnRequest.CompletedAt.HasValue)
            {
                timeline.Add(new OrderTimelineEventResponse
                {
                    Event = "ReturnCompleted",
                    OccurredAt = returnRequest.CompletedAt.Value,
                    Description = "Return was completed."
                });
            }
        }

        foreach (var refund in order.Refunds)
        {
            if (refund.CompletedAt.HasValue)
            {
                timeline.Add(new OrderTimelineEventResponse
                {
                    Event = "RefundCompleted",
                    OccurredAt = refund.CompletedAt.Value,
                    Description = "Refund was completed."
                });
            }
        }

        return Ok(timeline.OrderBy(item => item.OccurredAt).ToList());
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("{id:guid}/admin-summary")]
    public async Task<ActionResult<AdminOrderSummaryResponse>> GetAdminSummary(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Coupon)
            .Include(order => order.Items)
            .ThenInclude(item => item.ProductVariant)
            .Include(order => order.InventoryReservations)
            .ThenInclude(reservation => reservation.Product)
            .Include(order => order.Payments)
            .Include(order => order.Refunds)
            .Include(order => order.ReturnRequests)
            .Include(order => order.StatusHistories)
            .ThenInclude(history => history.ChangedByUser)
            .Include(order => order.InternalNotes)
            .ThenInclude(note => note.AuthorUser)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        return Ok(new AdminOrderSummaryResponse
        {
            Order = ToResponse(order),
            PaymentsCount = order.Payments.Count,
            PaymentsTotalAmount = order.Payments.Sum(payment => payment.Amount),
            RefundsCount = order.Refunds.Count,
            RefundedAmount = order.Refunds.Sum(refund => refund.Amount),
            ReturnRequestsCount = order.ReturnRequests.Count,
            StatusHistory = order.StatusHistories
                .OrderBy(history => history.CreatedAt)
                .Select(ToStatusHistoryResponse)
                .ToList(),
            InternalNotes = order.InternalNotes
                .OrderByDescending(note => note.CreatedAt)
                .Select(ToInternalNoteResponse)
                .ToList()
        });
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("{id:guid}/status-history")]
    public async Task<ActionResult<IReadOnlyList<OrderStatusHistoryResponse>>> GetStatusHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var orderExists = await dbContext.Orders
            .AsNoTracking()
            .AnyAsync(order => order.Id == id, cancellationToken);

        if (!orderExists)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        var historyItems = await dbContext.OrderStatusHistories
            .AsNoTracking()
            .Include(item => item.ChangedByUser)
            .Where(item => item.OrderId == id)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(historyItems
            .Select(ToStatusHistoryResponse)
            .ToList());
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("{id:guid}/internal-notes")]
    public async Task<ActionResult<IReadOnlyList<OrderInternalNoteResponse>>> GetInternalNotes(
        Guid id,
        CancellationToken cancellationToken)
    {
        var orderExists = await dbContext.Orders
            .AsNoTracking()
            .AnyAsync(order => order.Id == id, cancellationToken);

        if (!orderExists)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        var notes = await dbContext.OrderInternalNotes
            .AsNoTracking()
            .Include(note => note.AuthorUser)
            .Where(note => note.OrderId == id)
            .OrderByDescending(note => note.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(notes
            .Select(ToInternalNoteResponse)
            .ToList());
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost("{id:guid}/internal-notes")]
    public async Task<ActionResult<OrderInternalNoteResponse>> AddInternalNote(
        Guid id,
        AddOrderInternalNoteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Internal note is required."));
        }

        var orderExists = await dbContext.Orders
            .AsNoTracking()
            .AnyAsync(order => order.Id == id, cancellationToken);

        if (!orderExists)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Order was not found."));
        }

        var note = new OrderInternalNote
        {
            Id = Guid.NewGuid(),
            OrderId = id,
            AuthorUserId = userId.Value,
            Note = request.Note.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.OrderInternalNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);

        var createdNote = await dbContext.OrderInternalNotes
            .AsNoTracking()
            .Include(item => item.AuthorUser)
            .FirstAsync(item => item.Id == note.Id, cancellationToken);

        var response = ToInternalNoteResponse(createdNote);

        return CreatedAtAction(
            nameof(GetInternalNotes),
            new { id },
            response);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost("{id:guid}/admin-cancel")]
    public async Task<ActionResult<OrderStateChangedResponse>> AdminCancel(
        Guid id,
        AdminCancelOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var result = await orderFulfillmentService.CancelAsync(
            id,
            new CancelOrderRequest
            {
                Reason = request?.Reason
            },
            userId.Value,
            canSeeAllOrders: true,
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (result.Result is not OkObjectResult)
        {
            return result;
        }

        if (!string.IsNullOrWhiteSpace(request?.InternalNote))
        {
            dbContext.OrderInternalNotes.Add(new OrderInternalNote
            {
                Id = Guid.NewGuid(),
                OrderId = id,
                AuthorUserId = userId.Value,
                Note = request.InternalNote.Trim(),
                CreatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OrderStateChangedResponse>> Cancel(
        Guid id,
        CancelOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await orderFulfillmentService.CancelAsync(
            id,
            request,
            userId.Value,
            CanSeeAllOrders(),
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.OrderManagerOnly)]
    [HttpPost("{id:guid}/mark-processing")]
    public async Task<ActionResult<OrderStateChangedResponse>> MarkProcessing(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await orderFulfillmentService.MarkProcessingAsync(
            id,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.OrderManagerOnly)]
    [HttpPost("{id:guid}/mark-shipped")]
    public async Task<ActionResult<OrderStateChangedResponse>> MarkShipped(
        Guid id,
        ShipOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await orderFulfillmentService.MarkShippedAsync(
            id,
            request,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.OrderManagerOnly)]
    [HttpPost("{id:guid}/mark-delivered")]
    public async Task<ActionResult<OrderStateChangedResponse>> MarkDelivered(
        Guid id,
        DeliverOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        return await orderFulfillmentService.MarkDeliveredAsync(
            id,
            request,
            userId.Value,
            HttpContext.TraceIdentifier,
            cancellationToken);
    }

    private bool CanSeeAllOrders()
    {
        return currentUserService.IsInRole(ApplicationRoles.Admin) ||
               currentUserService.IsInRole(ApplicationRoles.OrderManager);
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

    private static void AddLegacyStatusTimelineEvents(
        Order order,
        List<OrderTimelineEventResponse> timeline)
    {
        if (order.PaidAt.HasValue)
        {
            timeline.Add(new OrderTimelineEventResponse
            {
                Event = "OrderPaid",
                OccurredAt = order.PaidAt.Value,
                Description = "Order payment was confirmed."
            });
        }

        if (order.ShippedAt.HasValue)
        {
            timeline.Add(new OrderTimelineEventResponse
            {
                Event = "OrderShipped",
                OccurredAt = order.ShippedAt.Value,
                Description = "Order was shipped."
            });
        }

        if (order.DeliveredAt.HasValue)
        {
            timeline.Add(new OrderTimelineEventResponse
            {
                Event = "OrderDelivered",
                OccurredAt = order.DeliveredAt.Value,
                Description = "Order was delivered."
            });
        }

        if (order.CancelledAt.HasValue)
        {
            timeline.Add(new OrderTimelineEventResponse
            {
                Event = "OrderCancelled",
                OccurredAt = order.CancelledAt.Value,
                Description = "Order was cancelled."
            });
        }
    }

    private static OrderResponse ToResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            Status = order.Status.ToString(),
            Subtotal = order.Subtotal,
            DiscountAmount = order.DiscountAmount,
            ShippingFee = order.ShippingFee,
            Total = order.Total,
            CouponId = order.CouponId,
            CouponCode = order.Coupon?.Code,
            CreatedAt = order.CreatedAt,
            PaidAt = order.PaidAt,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt,
            CancelledAt = order.CancelledAt,
            CancellationReason = order.CancellationReason,
            ShippingMethodId = order.ShippingMethodId,
            ShippingMethodName = order.ShippingMethodNameSnapshot,
            ShippingMethodCode = order.ShippingMethodCodeSnapshot,
            ShippingEstimatedDeliveryDays = order.ShippingEstimatedDeliveryDays,
            ShippingStatus = order.ShippingStatus.ToString(),
            ShippingCarrier = order.ShippingCarrier,
            TrackingNumber = order.TrackingNumber,
            DeliveryNote = order.DeliveryNote,
            ShippingAddressId = order.ShippingAddressId,
            ShippingFullName = order.ShippingFullName,
            ShippingPhoneNumber = order.ShippingPhoneNumber,
            ShippingCountry = order.ShippingCountry,
            ShippingCity = order.ShippingCity,
            ShippingArea = order.ShippingArea,
            ShippingStreet = order.ShippingStreet,
            ShippingBuilding = order.ShippingBuilding,
            ShippingFloor = order.ShippingFloor,
            ShippingApartment = order.ShippingApartment,
            ShippingPostalCode = order.ShippingPostalCode,
            ShippingNotes = order.ShippingNotes,
            Items = order.Items
                .OrderBy(item => item.ProductNameSnapshot)
                .Select(item => new OrderItemResponse
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductNameSnapshot = item.ProductNameSnapshot,
                    ProductSkuSnapshot = item.ProductSkuSnapshot,
                    ProductVariantId = item.ProductVariantId,
                    VariantNameSnapshot = item.VariantNameSnapshot,
                    VariantSkuSnapshot = item.VariantSkuSnapshot,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    Total = item.Total
                })
                .ToList(),
            Reservations = order.InventoryReservations
                .OrderBy(reservation => reservation.CreatedAt)
                .Select(reservation => new OrderReservationResponse
                {
                    Id = reservation.Id,
                    ProductId = reservation.ProductId,
                    ProductName = reservation.Product.Name,
                    ProductVariantId = reservation.ProductVariantId,
                    ProductVariantName = reservation.ProductVariant?.Name,
                    Quantity = reservation.Quantity,
                    Status = reservation.Status.ToString(),
                    CreatedAt = reservation.CreatedAt,
                    ExpiresAt = reservation.ExpiresAt,
                    ConfirmedAt = reservation.ConfirmedAt,
                    ReleasedAt = reservation.ReleasedAt,
                    ExpiredAt = reservation.ExpiredAt
                })
                .ToList()
        };
    }

    private static OrderStatusHistoryResponse ToStatusHistoryResponse(OrderStatusHistory history)
    {
        return new OrderStatusHistoryResponse
        {
            Id = history.Id,
            OrderId = history.OrderId,
            PreviousStatus = history.PreviousStatus?.ToString(),
            NewStatus = history.NewStatus.ToString(),
            ChangedByUserId = history.ChangedByUserId,
            ChangedByFullName = history.ChangedByUser?.FullName,
            ChangedByEmail = history.ChangedByUser?.Email,
            Reason = history.Reason,
            Note = history.Note,
            CreatedAt = history.CreatedAt
        };
    }

    private static OrderInternalNoteResponse ToInternalNoteResponse(OrderInternalNote note)
    {
        return new OrderInternalNoteResponse
        {
            Id = note.Id,
            OrderId = note.OrderId,
            AuthorUserId = note.AuthorUserId,
            AuthorFullName = note.AuthorUser.FullName,
            AuthorEmail = note.AuthorUser.Email,
            Note = note.Note,
            CreatedAt = note.CreatedAt
        };
    }
}
