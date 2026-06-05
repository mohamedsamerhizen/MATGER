using System.Security.Claims;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Coupons;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/coupons")]
[Authorize]
public sealed class CouponsController(
    ApplicationDbContext dbContext,
    ICouponService couponService,
    IAuditLogService auditLogService) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<CouponResponse>>> GetAll(
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);

        var query = dbContext.Coupons
            .AsNoTracking()
            .AsQueryable();

        var totalCount = await query.CountAsync();

        var coupons = await query
            .OrderBy(coupon => coupon.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(coupon => ToResponse(coupon))
            .ToListAsync();

        return Ok(PaginatedResponse<CouponResponse>.Create(
            coupons,
            page,
            pageSize,
            totalCount));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CouponResponse>> GetById(Guid id)
    {
        var coupon = await dbContext.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(coupon => coupon.Id == id);

        if (coupon is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Coupon was not found."));
        }

        return Ok(ToResponse(coupon));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    public async Task<ActionResult<CouponResponse>> Create(CreateCouponRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var validationError = ValidateCreateRequest(request);

        if (validationError is not null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validationError));
        }

        var normalizedCode = couponService.NormalizeCode(request.Code);

        var codeExists = await dbContext.Coupons
            .AnyAsync(coupon => coupon.Code == normalizedCode);

        if (codeExists)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Coupon code already exists."));
        }

        var now = DateTime.UtcNow;

        var coupon = new Coupon
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MaxDiscountAmount = request.MaxDiscountAmount,
            MinimumOrderSubtotal = request.MinimumOrderSubtotal,
            StartsAt = request.StartsAt,
            ExpiresAt = request.ExpiresAt,
            IsActive = true,
            UsageLimit = request.UsageLimit,
            UsageCount = 0,
            PerCustomerUsageLimit = request.PerCustomerUsageLimit,
            CreatedAt = now
        };

        dbContext.Coupons.Add(coupon);

        await auditLogService.LogAsync(
            actorUserId: userId.Value,
            action: "CouponCreated",
            entityName: nameof(Coupon),
            entityId: coupon.Id.ToString(),
            oldValue: null,
            newValue: ToAuditSnapshot(coupon),
            reason: "Coupon was created.");

        await dbContext.SaveChangesAsync();

        var response = ToResponse(coupon);

        return CreatedAtAction(
            nameof(GetById),
            new { id = coupon.Id },
            response);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<CouponResponse>> Update(
        Guid id,
        UpdateCouponRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var coupon = await dbContext.Coupons
            .FirstOrDefaultAsync(coupon => coupon.Id == id);

        if (coupon is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Coupon was not found."));
        }

        var validationError = ValidateUpdateRequest(coupon, request);

        if (validationError is not null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validationError));
        }

        var oldValue = ToAuditSnapshot(coupon);

        if (request.Name is not null)
        {
            coupon.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            coupon.Description = NormalizeOptional(request.Description);
        }

        if (request.DiscountValue.HasValue)
        {
            coupon.DiscountValue = request.DiscountValue.Value;
        }

        if (request.MaxDiscountAmount.HasValue)
        {
            coupon.MaxDiscountAmount = request.MaxDiscountAmount.Value;
        }
        else if (coupon.DiscountType == CouponDiscountType.FixedAmount)
        {
            coupon.MaxDiscountAmount = null;
        }

        if (request.MinimumOrderSubtotal.HasValue)
        {
            coupon.MinimumOrderSubtotal = request.MinimumOrderSubtotal.Value;
        }

        if (request.StartsAt.HasValue)
        {
            coupon.StartsAt = request.StartsAt.Value;
        }

        if (request.ExpiresAt.HasValue)
        {
            coupon.ExpiresAt = request.ExpiresAt;
        }

        if (request.UsageLimit.HasValue)
        {
            coupon.UsageLimit = request.UsageLimit;
        }

        if (request.PerCustomerUsageLimit.HasValue)
        {
            coupon.PerCustomerUsageLimit = request.PerCustomerUsageLimit;
        }

        if (request.IsActive.HasValue)
        {
            coupon.IsActive = request.IsActive.Value;
        }

        coupon.UpdatedAt = DateTime.UtcNow;

        await auditLogService.LogAsync(
            actorUserId: userId.Value,
            action: "CouponUpdated",
            entityName: nameof(Coupon),
            entityId: coupon.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(coupon),
            reason: "Coupon was updated.");

        await dbContext.SaveChangesAsync();

        return Ok(ToResponse(coupon));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var coupon = await dbContext.Coupons
            .FirstOrDefaultAsync(coupon => coupon.Id == id);

        if (coupon is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Coupon was not found."));
        }

        var oldValue = ToAuditSnapshot(coupon);

        coupon.IsActive = false;
        coupon.UpdatedAt = DateTime.UtcNow;

        await auditLogService.LogAsync(
            actorUserId: userId.Value,
            action: "CouponDisabled",
            entityName: nameof(Coupon),
            entityId: coupon.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(coupon),
            reason: "Coupon was disabled.");

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var coupon = await dbContext.Coupons
            .FirstOrDefaultAsync(coupon => coupon.Id == id);

        if (coupon is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Coupon was not found."));
        }

        var oldValue = ToAuditSnapshot(coupon);

        coupon.IsActive = true;
        coupon.UpdatedAt = DateTime.UtcNow;

        await auditLogService.LogAsync(
            actorUserId: userId.Value,
            action: "CouponEnabled",
            entityName: nameof(Coupon),
            entityId: coupon.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(coupon),
            reason: "Coupon was enabled.");

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/clear-expiry")]
    public async Task<ActionResult<CouponResponse>> ClearExpiry(Guid id)
    {
        return await ClearCouponValueAsync(
            id,
            "CouponExpiryCleared",
            "Coupon expiry was cleared.",
            coupon => coupon.ExpiresAt = null);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/clear-usage-limit")]
    public async Task<ActionResult<CouponResponse>> ClearUsageLimit(Guid id)
    {
        return await ClearCouponValueAsync(
            id,
            "CouponUsageLimitCleared",
            "Coupon usage limit was cleared.",
            coupon => coupon.UsageLimit = null);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/clear-per-customer-limit")]
    public async Task<ActionResult<CouponResponse>> ClearPerCustomerLimit(Guid id)
    {
        return await ClearCouponValueAsync(
            id,
            "CouponPerCustomerLimitCleared",
            "Coupon per-customer usage limit was cleared.",
            coupon => coupon.PerCustomerUsageLimit = null);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/clear-max-discount")]
    public async Task<ActionResult<CouponResponse>> ClearMaxDiscount(Guid id)
    {
        return await ClearCouponValueAsync(
            id,
            "CouponMaxDiscountCleared",
            "Coupon max discount amount was cleared.",
            coupon => coupon.MaxDiscountAmount = null,
            coupon => coupon.DiscountType == CouponDiscountType.Percentage,
            "Max discount amount can only be cleared for percentage coupons.");
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ValidateCouponResponse>> Validate(
        ValidateCouponRequest request)
    {
        var response = await couponService.ValidateAsync(
            request.Code,
            request.Subtotal,
            GetCurrentUserId());

        return Ok(response);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return userId;
    }

    private async Task<ActionResult<CouponResponse>> ClearCouponValueAsync(
        Guid id,
        string action,
        string reason,
        Action<Coupon> clearValue,
        Func<Coupon, bool>? canClear = null,
        string? invalidMessage = null)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var coupon = await dbContext.Coupons
            .FirstOrDefaultAsync(coupon => coupon.Id == id);

        if (coupon is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Coupon was not found."));
        }

        if (canClear is not null && !canClear(coupon))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                invalidMessage ?? "Coupon clear operation is invalid."));
        }

        var oldValue = ToAuditSnapshot(coupon);

        clearValue(coupon);
        coupon.UpdatedAt = DateTime.UtcNow;

        await auditLogService.LogAsync(
            actorUserId: userId.Value,
            action: action,
            entityName: nameof(Coupon),
            entityId: coupon.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(coupon),
            reason: reason);

        await dbContext.SaveChangesAsync();

        return Ok(ToResponse(coupon));
    }

    private static string? ValidateCreateRequest(CreateCouponRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Coupon code is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Coupon name is required.";
        }

        if (!Enum.IsDefined(request.DiscountType))
        {
            return "Coupon discount type is invalid.";
        }

        return ValidateCouponBusinessRules(
            request.DiscountType,
            request.DiscountValue,
            request.MaxDiscountAmount,
            request.MinimumOrderSubtotal,
            request.StartsAt,
            request.ExpiresAt,
            request.UsageLimit,
            request.PerCustomerUsageLimit,
            currentUsageCount: 0);
    }

    private static string? ValidateUpdateRequest(
        Coupon coupon,
        UpdateCouponRequest request)
    {
        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            return "Coupon name is required.";
        }

        if (coupon.DiscountType == CouponDiscountType.FixedAmount &&
            request.MaxDiscountAmount.HasValue)
        {
            return "Max discount amount only applies to percentage coupons.";
        }

        var discountValue = request.DiscountValue ?? coupon.DiscountValue;
        var maxDiscountAmount = coupon.DiscountType == CouponDiscountType.FixedAmount
            ? null
            : request.MaxDiscountAmount ?? coupon.MaxDiscountAmount;
        var minimumOrderSubtotal = request.MinimumOrderSubtotal ?? coupon.MinimumOrderSubtotal;
        var startsAt = request.StartsAt ?? coupon.StartsAt;
        var expiresAt = request.ExpiresAt ?? coupon.ExpiresAt;
        var usageLimit = request.UsageLimit ?? coupon.UsageLimit;
        var perCustomerUsageLimit = request.PerCustomerUsageLimit ?? coupon.PerCustomerUsageLimit;

        return ValidateCouponBusinessRules(
            coupon.DiscountType,
            discountValue,
            maxDiscountAmount,
            minimumOrderSubtotal,
            startsAt,
            expiresAt,
            usageLimit,
            perCustomerUsageLimit,
            coupon.UsageCount);
    }

    private static string? ValidateCouponBusinessRules(
        CouponDiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount,
        decimal minimumOrderSubtotal,
        DateTime startsAt,
        DateTime? expiresAt,
        int? usageLimit,
        int? perCustomerUsageLimit,
        int currentUsageCount)
    {
        if (discountType == CouponDiscountType.Percentage)
        {
            if (discountValue <= 0 || discountValue > 100)
            {
                return "Percentage coupon discount value must be greater than 0 and less than or equal to 100.";
            }

            if (maxDiscountAmount.HasValue && maxDiscountAmount.Value <= 0)
            {
                return "Max discount amount must be greater than 0.";
            }
        }
        else if (discountType == CouponDiscountType.FixedAmount)
        {
            if (discountValue <= 0)
            {
                return "Fixed amount coupon discount value must be greater than 0.";
            }

            if (maxDiscountAmount.HasValue)
            {
                return "Max discount amount only applies to percentage coupons.";
            }
        }
        else
        {
            return "Coupon discount type is invalid.";
        }

        if (minimumOrderSubtotal < 0)
        {
            return "Minimum order subtotal cannot be negative.";
        }

        if (startsAt == default)
        {
            return "Coupon start date is required.";
        }

        if (expiresAt.HasValue && expiresAt.Value <= startsAt)
        {
            return "Coupon expiry date must be after the start date.";
        }

        if (usageLimit.HasValue)
        {
            if (usageLimit.Value <= 0)
            {
                return "Usage limit must be greater than 0.";
            }

            if (usageLimit.Value < currentUsageCount)
            {
                return "Usage limit cannot be lower than the current usage count.";
            }
        }

        if (perCustomerUsageLimit.HasValue && perCustomerUsageLimit.Value <= 0)
        {
            return "Per-customer usage limit must be greater than 0.";
        }

        return null;
    }

    private static (int Page, int PageSize) NormalizePagination(int page, int pageSize)
    {
        return (
            Math.Max(page, DefaultPage),
            Math.Clamp(pageSize, 1, MaxPageSize));
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static CouponResponse ToResponse(Coupon coupon)
    {
        return new CouponResponse
        {
            Id = coupon.Id,
            Code = coupon.Code,
            Name = coupon.Name,
            Description = coupon.Description,
            DiscountType = coupon.DiscountType,
            DiscountValue = coupon.DiscountValue,
            MaxDiscountAmount = coupon.MaxDiscountAmount,
            MinimumOrderSubtotal = coupon.MinimumOrderSubtotal,
            StartsAt = coupon.StartsAt,
            ExpiresAt = coupon.ExpiresAt,
            IsActive = coupon.IsActive,
            UsageLimit = coupon.UsageLimit,
            UsageCount = coupon.UsageCount,
            PerCustomerUsageLimit = coupon.PerCustomerUsageLimit,
            CreatedAt = coupon.CreatedAt,
            UpdatedAt = coupon.UpdatedAt
        };
    }

    private static object ToAuditSnapshot(Coupon coupon)
    {
        return new
        {
            coupon.Id,
            coupon.Code,
            coupon.Name,
            coupon.Description,
            DiscountType = coupon.DiscountType.ToString(),
            coupon.DiscountValue,
            coupon.MaxDiscountAmount,
            coupon.MinimumOrderSubtotal,
            coupon.StartsAt,
            coupon.ExpiresAt,
            coupon.IsActive,
            coupon.UsageLimit,
            coupon.UsageCount,
            coupon.PerCustomerUsageLimit,
            coupon.CreatedAt,
            coupon.UpdatedAt
        };
    }
}