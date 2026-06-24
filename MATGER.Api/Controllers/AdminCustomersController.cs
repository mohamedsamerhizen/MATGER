using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Customers;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/customers")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminCustomersController(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("{userId:guid}/profile")]
    public async Task<ActionResult<CustomerProfileResponse>> GetProfile(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.CreatedAt,
                user.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Customer was not found."));
        }

        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId == userId)
            .Select(order => new
            {
                order.Id,
                order.Status,
                order.Total,
                order.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var revenueOrders = orders
            .Where(order => IsRevenueOrder(order.Status))
            .ToList();
        var totalSpent = revenueOrders.Sum(order => order.Total);
        var averageOrderValue = revenueOrders.Count == 0
            ? 0m
            : Math.Round(totalSpent / revenueOrders.Count, 2, MidpointRounding.AwayFromZero);
        var lastOrderDate = orders.Count == 0
            ? (DateTime?)null
            : orders.Max(order => order.CreatedAt);

        var returnOrderIds = await dbContext.ReturnRequests
            .AsNoTracking()
            .Where(returnRequest => returnRequest.UserId == userId)
            .Select(returnRequest => returnRequest.OrderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var refundOrderIds = await dbContext.Refunds
            .AsNoTracking()
            .Where(refund => refund.Order.UserId == userId)
            .Select(refund => refund.OrderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var affectedOrderIds = returnOrderIds
            .Concat(refundOrderIds)
            .ToHashSet();
        var returnRatio = orders.Count == 0
            ? 0m
            : Math.Round((decimal)affectedOrderIds.Count / orders.Count, 4, MidpointRounding.AwayFromZero);

        var refundsCount = await dbContext.Refunds
            .AsNoTracking()
            .CountAsync(refund => refund.Order.UserId == userId, cancellationToken);
        var reviewsCount = await dbContext.ProductReviews
            .AsNoTracking()
            .CountAsync(review => review.UserId == userId, cancellationToken);
        var wishlistCount = await dbContext.WishlistItems
            .AsNoTracking()
            .CountAsync(item => item.UserId == userId, cancellationToken);

        var activeCart = await dbContext.Carts
            .AsNoTracking()
            .Include(cart => cart.Items)
            .Where(cart =>
                cart.UserId == userId &&
                cart.Status == CartStatus.Active)
            .OrderByDescending(cart => cart.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var activeCartSummary = ToActiveCartSummary(activeCart);
        var segment = ResolveSegment(
            ordersCount: orders.Count,
            totalSpent: totalSpent,
            lastOrderDate: lastOrderDate,
            refundsCount: refundsCount,
            returnRatio: returnRatio,
            customerCreatedAt: customer.CreatedAt,
            now: DateTime.UtcNow);
        var riskLevel = ResolveRiskLevel(segment, refundsCount, returnRatio);

        return Ok(new CustomerProfileResponse
        {
            UserId = customer.Id,
            FullName = customer.FullName,
            Email = customer.Email ?? string.Empty,
            PhoneNumber = customer.PhoneNumber,
            CreatedAtUtc = customer.CreatedAt,
            IsActive = customer.IsActive,
            OrdersCount = orders.Count,
            TotalSpent = totalSpent,
            AverageOrderValue = averageOrderValue,
            LastOrderDate = lastOrderDate,
            RefundsCount = refundsCount,
            ReturnRatio = returnRatio,
            ReviewsCount = reviewsCount,
            WishlistCount = wishlistCount,
            ActiveCart = activeCartSummary,
            CustomerSegment = segment,
            RiskLevel = riskLevel
        });
    }

    [HttpGet("{userId:guid}/notes")]
    public async Task<ActionResult<IReadOnlyList<CustomerInternalNoteResponse>>> ListNotes(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await CustomerExistsAsync(userId, cancellationToken))
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Customer was not found."));
        }

        var notes = await dbContext.CustomerInternalNotes
            .AsNoTracking()
            .Include(note => note.CreatedByUser)
            .Where(note => note.CustomerId == userId)
            .OrderByDescending(note => note.IsImportant)
            .ThenByDescending(note => note.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(notes.Select(ToResponse).ToList());
    }

    [HttpPost("{userId:guid}/notes")]
    public async Task<ActionResult<CustomerInternalNoteResponse>> AddNote(
        Guid userId,
        CreateCustomerInternalNoteRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = currentUserService.UserId;

        if (actorUserId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        if (!await CustomerExistsAsync(userId, cancellationToken))
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Customer was not found."));
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Internal note is required."));
        }

        var note = new CustomerInternalNote
        {
            Id = Guid.NewGuid(),
            CustomerId = userId,
            CreatedByUserId = actorUserId.Value,
            Note = request.Note.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            IsImportant = request.IsImportant
        };

        dbContext.CustomerInternalNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await LoadNoteResponseAsync(note.Id, cancellationToken);

        return CreatedAtAction(
            nameof(ListNotes),
            new { userId },
            response);
    }

    [HttpDelete("{userId:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var note = await dbContext.CustomerInternalNotes
            .FirstOrDefaultAsync(item =>
                item.Id == noteId &&
                item.CustomerId == userId,
                cancellationToken);

        if (note is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Customer internal note was not found."));
        }

        dbContext.CustomerInternalNotes.Remove(note);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<bool> CustomerExistsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);
    }

    private async Task<CustomerInternalNoteResponse> LoadNoteResponseAsync(
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var note = await dbContext.CustomerInternalNotes
            .AsNoTracking()
            .Include(item => item.CreatedByUser)
            .FirstAsync(item => item.Id == noteId, cancellationToken);

        return ToResponse(note);
    }

    private static CustomerInternalNoteResponse ToResponse(CustomerInternalNote note)
    {
        return new CustomerInternalNoteResponse
        {
            Id = note.Id,
            CustomerId = note.CustomerId,
            Note = note.Note,
            CreatedByUserId = note.CreatedByUserId,
            CreatedByUserName = note.CreatedByUser.FullName,
            CreatedAtUtc = note.CreatedAtUtc,
            IsImportant = note.IsImportant
        };
    }

    private static CustomerActiveCartSummaryResponse ToActiveCartSummary(Cart? activeCart)
    {
        if (activeCart is null)
        {
            return new CustomerActiveCartSummaryResponse();
        }

        var subtotal = activeCart.Items.Sum(item => item.UnitPriceSnapshot * item.Quantity);
        var total = Math.Max(0m, subtotal - activeCart.DiscountAmount);

        return new CustomerActiveCartSummaryResponse
        {
            CartId = activeCart.Id,
            ItemsCount = activeCart.Items.Count,
            TotalQuantity = activeCart.Items.Sum(item => item.Quantity),
            Subtotal = subtotal,
            DiscountAmount = activeCart.DiscountAmount,
            Total = total
        };
    }

    private static bool IsRevenueOrder(OrderStatus status)
    {
        return status is OrderStatus.Paid
            or OrderStatus.Processing
            or OrderStatus.Shipped
            or OrderStatus.Delivered
            or OrderStatus.ReturnRequested
            or OrderStatus.Returned
            or OrderStatus.Refunded;
    }

    private static string ResolveSegment(
        int ordersCount,
        decimal totalSpent,
        DateTime? lastOrderDate,
        int refundsCount,
        decimal returnRatio,
        DateTime customerCreatedAt,
        DateTime now)
    {
        if (ordersCount > 0 && (refundsCount > 0 || returnRatio >= 0.25m))
        {
            return "HighRefund";
        }

        if (ordersCount >= 5 || totalSpent >= 250000m)
        {
            return "VIP";
        }

        if ((ordersCount == 0 && customerCreatedAt <= now.AddDays(-60)) ||
            (lastOrderDate.HasValue && lastOrderDate.Value <= now.AddDays(-75)))
        {
            return "Dormant";
        }

        if (ordersCount > 0 &&
            lastOrderDate.HasValue &&
            lastOrderDate.Value <= now.AddDays(-45))
        {
            return "AtRisk";
        }

        if (ordersCount <= 1 && customerCreatedAt >= now.AddDays(-30))
        {
            return "New";
        }

        return "Active";
    }

    private static string ResolveRiskLevel(
        string segment,
        int refundsCount,
        decimal returnRatio)
    {
        if (segment == "HighRefund" &&
            (refundsCount >= 3 || returnRatio >= 0.5m))
        {
            return "Critical";
        }

        if (segment == "HighRefund" || returnRatio >= 0.25m)
        {
            return "High";
        }

        if (segment is "AtRisk" or "Dormant" || returnRatio > 0m)
        {
            return "Medium";
        }

        return "Low";
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
