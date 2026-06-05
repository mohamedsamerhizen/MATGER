using MATGER.Api.Data;
using MATGER.Api.DTOs.Coupons;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class CouponService(ApplicationDbContext dbContext) : ICouponService
{
    public string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }

    public async Task<ValidateCouponResponse> ValidateAsync(
        string code,
        decimal subtotal,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code)
            ? string.Empty
            : NormalizeCode(code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return Invalid(normalizedCode, "Coupon code is required.");
        }

        if (subtotal < 0)
        {
            return Invalid(normalizedCode, "Subtotal cannot be negative.");
        }

        var coupon = await dbContext.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(
                coupon => coupon.Code == normalizedCode,
                cancellationToken);

        if (coupon is null)
        {
            return Invalid(normalizedCode, "Coupon was not found.");
        }

        var configurationError = ValidateCouponConfiguration(coupon);

        if (configurationError is not null)
        {
            return Invalid(coupon.Code, configurationError);
        }

        var now = DateTime.UtcNow;

        if (!coupon.IsActive)
        {
            return Invalid(coupon.Code, "Coupon is disabled.");
        }

        if (coupon.StartsAt > now)
        {
            return Invalid(coupon.Code, "Coupon is not active yet.");
        }

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value <= now)
        {
            return Invalid(coupon.Code, "Coupon has expired.");
        }

        if (subtotal < coupon.MinimumOrderSubtotal)
        {
            return Invalid(
                coupon.Code,
                $"Coupon requires a minimum subtotal of {coupon.MinimumOrderSubtotal:0.##}.");
        }

        if (coupon.UsageLimit.HasValue && coupon.UsageCount >= coupon.UsageLimit.Value)
        {
            return Invalid(coupon.Code, "Coupon usage limit has been reached.");
        }

        if (coupon.PerCustomerUsageLimit.HasValue && userId.HasValue)
        {
            var customerUsageCount = await dbContext.CouponRedemptions
                .AsNoTracking()
                .CountAsync(
                    redemption =>
                        redemption.CouponId == coupon.Id &&
                        redemption.UserId == userId.Value,
                    cancellationToken);

            if (customerUsageCount >= coupon.PerCustomerUsageLimit.Value)
            {
                return Invalid(coupon.Code, "Coupon customer usage limit has been reached.");
            }
        }

        var discountAmount = CalculateDiscount(coupon, subtotal);

        if (discountAmount <= 0)
        {
            return Invalid(coupon.Code, "Coupon does not apply a discount to this subtotal.");
        }

        return new ValidateCouponResponse
        {
            IsValid = true,
            CouponId = coupon.Id,
            Code = coupon.Code,
            DiscountAmount = discountAmount,
            Message = "Coupon is valid."
        };
    }

    private static string? ValidateCouponConfiguration(Coupon coupon)
    {
        if (coupon.DiscountType == CouponDiscountType.Percentage)
        {
            if (coupon.DiscountValue <= 0 || coupon.DiscountValue > 100)
            {
                return "Percentage coupon discount value is invalid.";
            }

            if (coupon.MaxDiscountAmount.HasValue && coupon.MaxDiscountAmount.Value <= 0)
            {
                return "Coupon max discount amount is invalid.";
            }
        }
        else if (coupon.DiscountType == CouponDiscountType.FixedAmount)
        {
            if (coupon.DiscountValue <= 0)
            {
                return "Fixed amount coupon discount value is invalid.";
            }

            if (coupon.MaxDiscountAmount.HasValue)
            {
                return "Fixed amount coupon cannot have a max discount amount.";
            }
        }
        else
        {
            return "Coupon discount type is invalid.";
        }

        if (coupon.MinimumOrderSubtotal < 0)
        {
            return "Coupon minimum order subtotal is invalid.";
        }

        if (coupon.StartsAt == default)
        {
            return "Coupon start date is invalid.";
        }

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value <= coupon.StartsAt)
        {
            return "Coupon expiry date is invalid.";
        }

        if (coupon.UsageLimit.HasValue && coupon.UsageLimit.Value <= 0)
        {
            return "Coupon usage limit is invalid.";
        }

        if (coupon.PerCustomerUsageLimit.HasValue &&
            coupon.PerCustomerUsageLimit.Value <= 0)
        {
            return "Coupon per-customer usage limit is invalid.";
        }

        return null;
    }

    private static decimal CalculateDiscount(Coupon coupon, decimal subtotal)
    {
        var discountAmount = coupon.DiscountType switch
        {
            CouponDiscountType.Percentage => subtotal * coupon.DiscountValue / 100m,
            CouponDiscountType.FixedAmount => coupon.DiscountValue,
            _ => 0m
        };

        if (coupon.DiscountType == CouponDiscountType.Percentage &&
            coupon.MaxDiscountAmount.HasValue)
        {
            discountAmount = Math.Min(discountAmount, coupon.MaxDiscountAmount.Value);
        }

        discountAmount = Math.Min(discountAmount, subtotal);
        discountAmount = Math.Max(discountAmount, 0m);

        return decimal.Round(discountAmount, 2, MidpointRounding.AwayFromZero);
    }

    private static ValidateCouponResponse Invalid(string code, string message)
    {
        return new ValidateCouponResponse
        {
            IsValid = false,
            Code = code,
            DiscountAmount = 0m,
            Message = message
        };
    }
}
