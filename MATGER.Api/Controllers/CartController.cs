using System.Security.Claims;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Cart;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
public sealed class CartController(
    ApplicationDbContext dbContext,
    ICouponService couponService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CartResponse>> Get()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var cart = await GetOrCreateActiveCartAsync(userId.Value);

        await RefreshCartCouponAsync(cart, userId.Value);
        await dbContext.SaveChangesAsync();

        return Ok(ToResponse(cart));
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> AddItem(AddCartItemRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        if (request.ProductId == Guid.Empty)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product id is required."));
        }

        if (request.ProductVariantId == Guid.Empty)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product variant id is invalid."));
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Quantity must be greater than zero."));
        }

        var product = await dbContext.Products
            .Include(product => product.Category)
            .Include(product => product.InventoryItem)
            .FirstOrDefaultAsync(product => product.Id == request.ProductId);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        if (!product.IsActive)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product is not active."));
        }

        if (!product.Category.IsActive)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product category is not active."));
        }

        ProductVariant? variant = null;

        if (request.ProductVariantId.HasValue)
        {
            variant = await dbContext.ProductVariants
                .FirstOrDefaultAsync(productVariant =>
                    productVariant.Id == request.ProductVariantId.Value &&
                    productVariant.ProductId == product.Id);

            if (variant is null)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product variant was not found."));
            }

            if (!variant.IsActive)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product variant is not active."));
            }
        }
        else if (product.InventoryItem is null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product inventory was not found."));
        }

        var cart = await GetOrCreateActiveCartAsync(userId.Value);

        var existingItem = cart.Items
            .FirstOrDefault(item =>
                item.ProductId == product.Id &&
                item.ProductVariantId == request.ProductVariantId);

        var requestedTotalQuantity = request.Quantity;

        if (existingItem is not null)
        {
            requestedTotalQuantity += existingItem.Quantity;
        }

        var availableQuantity = variant is null
            ? product.InventoryItem!.QuantityAvailable
            : variant.QuantityAvailable;

        if (requestedTotalQuantity > availableQuantity)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Requested quantity is greater than available stock."));
        }

        var now = DateTime.UtcNow;
        var unitPrice = variant?.PriceOverride ?? product.Price;

        if (existingItem is null)
        {
            var cartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = product.Id,
                ProductVariantId = variant?.Id,
                Quantity = request.Quantity,
                UnitPriceSnapshot = unitPrice,
                CreatedAt = now
            };

            dbContext.CartItems.Add(cartItem);
            dbContext.Entry(cartItem).State = EntityState.Added;
        }
        else
        {
            existingItem.Quantity += request.Quantity;
            existingItem.UnitPriceSnapshot = unitPrice;
            existingItem.UpdatedAt = now;
        }

        await RefreshCartCouponAsync(cart, userId.Value);

        await dbContext.SaveChangesAsync();

        var updatedCart = await LoadCartAsync(cart.Id);

        return Ok(ToResponse(updatedCart));
    }

    [HttpPatch("items/{itemId:guid}")]
    public async Task<ActionResult<CartResponse>> UpdateItem(
        Guid itemId,
        UpdateCartItemRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Quantity must be greater than zero."));
        }

        var cart = await GetOrCreateActiveCartAsync(userId.Value);

        var item = cart.Items.FirstOrDefault(item => item.Id == itemId);

        if (item is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Cart item was not found."));
        }

        var product = await dbContext.Products
            .Include(product => product.Category)
            .Include(product => product.InventoryItem)
            .FirstOrDefaultAsync(product => product.Id == item.ProductId);

        if (product is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Product was not found."));
        }

        if (!product.IsActive)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product is not active."));
        }

        if (!product.Category.IsActive)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product category is not active."));
        }

        ProductVariant? variant = null;

        if (item.ProductVariantId.HasValue)
        {
            variant = await dbContext.ProductVariants
                .FirstOrDefaultAsync(productVariant =>
                    productVariant.Id == item.ProductVariantId.Value &&
                    productVariant.ProductId == product.Id);

            if (variant is null)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product variant was not found."));
            }

            if (!variant.IsActive)
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Product variant is not active."));
            }
        }
        else if (product.InventoryItem is null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Product inventory was not found."));
        }

        var availableQuantity = variant is null
            ? product.InventoryItem!.QuantityAvailable
            : variant.QuantityAvailable;

        if (request.Quantity > availableQuantity)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Requested quantity is greater than available stock."));
        }

        item.Quantity = request.Quantity;
        item.UnitPriceSnapshot = variant?.PriceOverride ?? product.Price;
        item.UpdatedAt = DateTime.UtcNow;

        await RefreshCartCouponAsync(cart, userId.Value);

        await dbContext.SaveChangesAsync();

        var updatedCart = await LoadCartAsync(cart.Id);

        return Ok(ToResponse(updatedCart));
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartResponse>> RemoveItem(Guid itemId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var cart = await GetOrCreateActiveCartAsync(userId.Value);

        var item = cart.Items.FirstOrDefault(item => item.Id == itemId);

        if (item is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Cart item was not found."));
        }

        dbContext.CartItems.Remove(item);
        cart.Items.Remove(item);

        await RefreshCartCouponAsync(cart, userId.Value);

        await dbContext.SaveChangesAsync();

        var updatedCart = await LoadCartAsync(cart.Id);

        return Ok(ToResponse(updatedCart));
    }

    [HttpPost("coupon")]
    public async Task<ActionResult<CartResponse>> ApplyCoupon(ApplyCouponRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Coupon code is required."));
        }

        var cart = await GetOrCreateActiveCartAsync(userId.Value);

        if (cart.Items.Count == 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Cannot apply a coupon to an empty cart."));
        }

        var subtotal = CalculateSubtotal(cart);

        var validation = await couponService.ValidateAsync(
            request.Code,
            subtotal,
            userId.Value);

        if (!validation.IsValid)
        {
            ClearCoupon(cart);

            await dbContext.SaveChangesAsync();

            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validation.Message));
        }

        cart.CouponId = validation.CouponId;
        cart.CouponCodeSnapshot = validation.Code;
        cart.DiscountAmount = validation.DiscountAmount;

        await dbContext.SaveChangesAsync();

        var updatedCart = await LoadCartAsync(cart.Id);

        return Ok(ToResponse(updatedCart));
    }

    [HttpDelete("coupon")]
    public async Task<ActionResult<CartResponse>> RemoveCoupon()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var cart = await GetOrCreateActiveCartAsync(userId.Value);

        ClearCoupon(cart);

        await dbContext.SaveChangesAsync();

        var updatedCart = await LoadCartAsync(cart.Id);

        return Ok(ToResponse(updatedCart));
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

    private async Task<Cart> GetOrCreateActiveCartAsync(Guid userId)
    {
        var cart = await dbContext.Carts
            .Include(cart => cart.Coupon)
            .Include(cart => cart.Items)
            .ThenInclude(item => item.Product)
            .Include(cart => cart.Items)
            .ThenInclude(item => item.ProductVariant)
            .FirstOrDefaultAsync(cart =>
                cart.UserId == userId &&
                cart.Status == CartStatus.Active);

        if (cart is not null && cart.ExpiresAt <= DateTime.UtcNow)
        {
            cart.Status = CartStatus.Expired;
            await dbContext.SaveChangesAsync();

            cart = null;
        }

        if (cart is not null)
        {
            return cart;
        }

        cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = CartStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        dbContext.Carts.Add(cart);

        await dbContext.SaveChangesAsync();

        return await LoadCartAsync(cart.Id);
    }

    private async Task<Cart> LoadCartAsync(Guid cartId)
    {
        return await dbContext.Carts
            .Include(cart => cart.Coupon)
            .Include(cart => cart.Items)
            .ThenInclude(item => item.Product)
            .Include(cart => cart.Items)
            .ThenInclude(item => item.ProductVariant)
            .FirstAsync(cart => cart.Id == cartId);
    }

    private async Task RefreshCartCouponAsync(Cart cart, Guid userId)
    {
        if (!cart.CouponId.HasValue && string.IsNullOrWhiteSpace(cart.CouponCodeSnapshot))
        {
            cart.DiscountAmount = 0m;

            return;
        }

        if (cart.Items.Count == 0)
        {
            ClearCoupon(cart);

            return;
        }

        if (string.IsNullOrWhiteSpace(cart.CouponCodeSnapshot))
        {
            ClearCoupon(cart);

            return;
        }

        var validation = await couponService.ValidateAsync(
            cart.CouponCodeSnapshot,
            CalculateSubtotal(cart),
            userId);

        if (!validation.IsValid)
        {
            ClearCoupon(cart);

            return;
        }

        cart.CouponId = validation.CouponId;
        cart.CouponCodeSnapshot = validation.Code;
        cart.DiscountAmount = validation.DiscountAmount;
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

    private static void ClearCoupon(Cart cart)
    {
        cart.CouponId = null;
        cart.Coupon = null;
        cart.CouponCodeSnapshot = null;
        cart.DiscountAmount = 0m;
    }

    private static decimal CalculateSubtotal(Cart cart)
    {
        return cart.Items.Sum(item => item.UnitPriceSnapshot * item.Quantity);
    }

    private static CartResponse ToResponse(Cart cart)
    {
        return new CartResponse
        {
            Id = cart.Id,
            Status = cart.Status.ToString(),
            CreatedAt = cart.CreatedAt,
            ExpiresAt = cart.ExpiresAt,
            DiscountAmount = cart.DiscountAmount,
            CouponCode = cart.CouponCodeSnapshot,
            Items = cart.Items
                .OrderBy(item => item.CreatedAt)
                .Select(item => new CartItemResponse
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    SKU = item.Product.SKU,
                    ProductVariantId = item.ProductVariantId,
                    VariantName = item.ProductVariant?.Name,
                    VariantSku = item.ProductVariant?.SKU,
                    Quantity = item.Quantity,
                    UnitPriceSnapshot = item.UnitPriceSnapshot
                })
                .ToList()
        };
    }
}
