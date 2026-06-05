using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Checkout;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class CheckoutService(
    ApplicationDbContext dbContext,
    IAuditLogService auditLogService,
    ICouponService couponService,
    IInventoryMovementService inventoryMovementService) : ICheckoutService
{
    private const string StartCheckoutEndpoint = "POST /api/checkout/start";
    private const string ConfirmPaymentEndpoint = "POST /api/checkout/confirm-payment";
    private const string FailPaymentEndpoint = "POST /api/checkout/fail-payment";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ActionResult<CheckoutStartResponse>> StartCheckoutAsync(
        CheckoutStartRequest? request,
        string? idempotencyKey,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Idempotency-Key header is required.",
                traceId));
        }

        idempotencyKey = idempotencyKey.Trim();
        var requestHash = ComputeRequestHash(StartCheckoutEndpoint, request);

        var previousRecord = await GetPreviousIdempotencyRecordAsync(
            userId,
            StartCheckoutEndpoint,
            idempotencyKey,
            cancellationToken);

        if (previousRecord is not null)
        {
            return ToStoredOrConflictResult<CheckoutStartResponse>(
                previousRecord,
                requestHash,
                traceId);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        previousRecord = await GetPreviousIdempotencyRecordAsync(
            userId,
            StartCheckoutEndpoint,
            idempotencyKey,
            cancellationToken);

        if (previousRecord is not null)
        {
            await transaction.CommitAsync(cancellationToken);

            return ToStoredOrConflictResult<CheckoutStartResponse>(
                previousRecord,
                requestHash,
                traceId);
        }

        var cart = await dbContext.Carts
            .Include(cart => cart.Coupon)
            .Include(cart => cart.Items)
            .ThenInclude(item => item.Product)
            .ThenInclude(product => product.InventoryItem)
            .Include(cart => cart.Items)
            .ThenInclude(item => item.Product)
            .ThenInclude(product => product.Category)
            .Include(cart => cart.Items)
            .ThenInclude(item => item.ProductVariant)
            .FirstOrDefaultAsync(cart =>
                cart.UserId == userId &&
                cart.Status == CartStatus.Active,
                cancellationToken);

        if (cart is null)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Active cart was not found.",
                traceId));
        }

        if (cart.ExpiresAt <= DateTime.UtcNow)
        {
            cart.Status = CartStatus.Expired;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Cart has expired.",
                traceId));
        }

        if (cart.Items.Count == 0)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Cart is empty.",
                traceId));
        }

        foreach (var item in cart.Items)
        {
            if (!item.Product.IsActive || !item.Product.Category.IsActive)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    $"Product '{item.Product.Name}' is no longer available.",
                    traceId));
            }

            if (item.ProductVariantId.HasValue)
            {
                if (item.ProductVariant is null)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        $"Product variant for '{item.Product.Name}' was not found.",
                        traceId));
                }

                if (!item.ProductVariant.IsActive)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        $"Product variant '{item.ProductVariant.Name}' is no longer available.",
                        traceId));
                }

                if (item.Quantity > item.ProductVariant.QuantityAvailable)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        $"Insufficient stock for product variant '{item.ProductVariant.Name}'.",
                        traceId));
                }

                continue;
            }

            if (item.Product.InventoryItem is null)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    $"Product '{item.Product.Name}' has no inventory record.",
                    traceId));
            }

            if (item.Quantity > item.Product.InventoryItem.QuantityAvailable)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    $"Insufficient stock for product '{item.Product.Name}'.",
                    traceId));
            }
        }

        var shippingAddress = await LoadShippingAddressAsync(
            userId,
            request?.ShippingAddressId,
            cancellationToken);

        if (shippingAddress is null)
        {
            return request?.ShippingAddressId.HasValue == true
                ? new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Shipping address was not found.",
                    traceId))
                : new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Shipping address is required.",
                    traceId));
        }

        var shippingMethod = await LoadShippingMethodAsync(
            request?.ShippingMethodId,
            cancellationToken);

        if (shippingMethod is null && request?.ShippingMethodId.HasValue == true)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Shipping method was not found.",
                traceId));
        }

        var now = DateTime.UtcNow;
        var reservationExpiresAt = now.AddMinutes(15);

        var subtotal = cart.Items.Sum(item => item.UnitPriceSnapshot * item.Quantity);
        var discountAmount = 0m;
        var shippingFee = shippingMethod?.BaseCost ?? 0m;
        object? couponAuditSnapshot = null;

        if (cart.CouponId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(cart.CouponCodeSnapshot))
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Applied coupon code is missing.",
                    traceId));
            }

            var couponValidation = await couponService.ValidateAsync(
                cart.CouponCodeSnapshot,
                subtotal,
                userId,
                cancellationToken);

            if (!couponValidation.IsValid)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    couponValidation.Message,
                    traceId));
            }

            cart.CouponId = couponValidation.CouponId;
            cart.CouponCodeSnapshot = couponValidation.Code;
            cart.DiscountAmount = couponValidation.DiscountAmount;
            discountAmount = couponValidation.DiscountAmount;

            couponAuditSnapshot = new
            {
                CouponId = couponValidation.CouponId,
                CouponCode = couponValidation.Code,
                couponValidation.DiscountAmount
            };
        }
        else
        {
            cart.CouponCodeSnapshot = null;
            cart.DiscountAmount = 0m;
        }

        var total = Math.Max(subtotal - discountAmount + shippingFee, 0m);
        var reservedInventory = new List<object>();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            Status = OrderStatus.PendingPayment,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            ShippingFee = shippingFee,
            Total = total,
            CouponId = cart.CouponId,
            Coupon = cart.Coupon,
            ShippingMethodId = shippingMethod?.Id,
            ShippingMethodNameSnapshot = shippingMethod?.Name,
            ShippingMethodCodeSnapshot = shippingMethod?.Code,
            ShippingEstimatedDeliveryDays = shippingMethod?.EstimatedDeliveryDays,
            ShippingStatus = ShippingStatus.Pending,
            CreatedAt = now
        };

        ApplyShippingSnapshot(order, shippingAddress);

        foreach (var cartItem in cart.Items)
        {
            var orderItemTotal = cartItem.UnitPriceSnapshot * cartItem.Quantity;

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = cartItem.ProductId,
                Product = cartItem.Product,
                ProductVariantId = cartItem.ProductVariantId,
                ProductVariant = cartItem.ProductVariant,
                ProductNameSnapshot = cartItem.Product.Name,
                ProductSkuSnapshot = cartItem.Product.SKU,
                VariantNameSnapshot = cartItem.ProductVariant?.Name,
                VariantSkuSnapshot = cartItem.ProductVariant?.SKU,
                UnitPrice = cartItem.UnitPriceSnapshot,
                Quantity = cartItem.Quantity,
                Total = orderItemTotal
            });

            var reservation = new InventoryReservation
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = cartItem.ProductId,
                ProductVariantId = cartItem.ProductVariantId,
                Quantity = cartItem.Quantity,
                Status = InventoryReservationStatus.Pending,
                ExpiresAt = reservationExpiresAt,
                CreatedAt = now
            };

            order.InventoryReservations.Add(reservation);

            if (cartItem.ProductVariant is not null)
            {
                var quantityAvailableBefore = cartItem.ProductVariant.QuantityAvailable;
                var quantityReservedBefore = cartItem.ProductVariant.QuantityReserved;

                cartItem.ProductVariant.QuantityAvailable -= cartItem.Quantity;
                cartItem.ProductVariant.QuantityReserved += cartItem.Quantity;
                cartItem.ProductVariant.UpdatedAt = now;

                await inventoryMovementService.LogAsync(
                    productId: reservation.ProductId,
                    inventoryItemId: Guid.Empty,
                    type: InventoryMovementType.ReservationCreated,
                    quantityChange: -cartItem.Quantity,
                    quantityAvailableBefore: quantityAvailableBefore,
                    quantityAvailableAfter: cartItem.ProductVariant.QuantityAvailable,
                    quantityReservedBefore: quantityReservedBefore,
                    quantityReservedAfter: cartItem.ProductVariant.QuantityReserved,
                    reason: "Product variant inventory was reserved during checkout.",
                    referenceType: nameof(InventoryReservation),
                    referenceId: reservation.Id.ToString(),
                    actorUserId: userId,
                    createdAt: now,
                    cancellationToken: cancellationToken,
                    productVariantId: reservation.ProductVariantId);

                reservedInventory.Add(new
                {
                    ReservationId = reservation.Id,
                    reservation.ProductId,
                    reservation.ProductVariantId,
                    VariantName = cartItem.ProductVariant.Name,
                    reservation.Quantity,
                    Status = reservation.Status.ToString(),
                    reservation.ExpiresAt,
                    QuantityAvailableBefore = quantityAvailableBefore,
                    QuantityAvailableAfter = cartItem.ProductVariant.QuantityAvailable,
                    QuantityReservedBefore = quantityReservedBefore,
                    QuantityReservedAfter = cartItem.ProductVariant.QuantityReserved
                });

                continue;
            }

            var inventoryItem = cartItem.Product.InventoryItem!;
            var productQuantityAvailableBefore = inventoryItem.QuantityAvailable;
            var productQuantityReservedBefore = inventoryItem.QuantityReserved;

            inventoryItem.QuantityAvailable -= cartItem.Quantity;
            inventoryItem.QuantityReserved += cartItem.Quantity;
            inventoryItem.UpdatedAt = now;

            await inventoryMovementService.LogAsync(
                productId: inventoryItem.ProductId,
                inventoryItemId: inventoryItem.Id,
                type: InventoryMovementType.ReservationCreated,
                quantityChange: -cartItem.Quantity,
                quantityAvailableBefore: productQuantityAvailableBefore,
                quantityAvailableAfter: inventoryItem.QuantityAvailable,
                quantityReservedBefore: productQuantityReservedBefore,
                quantityReservedAfter: inventoryItem.QuantityReserved,
                reason: "Inventory was reserved during checkout.",
                referenceType: nameof(InventoryReservation),
                referenceId: reservation.Id.ToString(),
                actorUserId: userId,
                createdAt: now,
                cancellationToken: cancellationToken);

            reservedInventory.Add(new
            {
                ReservationId = reservation.Id,
                reservation.ProductId,
                reservation.Quantity,
                Status = reservation.Status.ToString(),
                reservation.ExpiresAt,
                InventoryItemId = inventoryItem.Id,
                QuantityAvailableBefore = productQuantityAvailableBefore,
                QuantityAvailableAfter = inventoryItem.QuantityAvailable,
                QuantityReservedBefore = productQuantityReservedBefore,
                QuantityReservedAfter = inventoryItem.QuantityReserved
            });
        }

        cart.Status = CartStatus.CheckedOut;

        dbContext.Orders.Add(order);

        AddStatusHistory(
            order,
            previousStatus: null,
            newStatus: order.Status,
            changedByUserId: userId,
            reason: "Checkout was started and order entered pending payment.",
            note: null,
            createdAt: now);

        await auditLogService.LogAsync(
            actorUserId: userId,
            action: "CheckoutStarted",
            entityName: nameof(Order),
            entityId: order.Id.ToString(),
            oldValue: null,
            newValue: new
            {
                order.Id,
                order.OrderNumber,
                Status = order.Status.ToString(),
                order.Subtotal,
                order.DiscountAmount,
                order.ShippingFee,
                order.Total,
                Coupon = couponAuditSnapshot,
                ShippingAddress = ToShippingAuditSnapshot(order),
                ItemsCount = order.Items.Count,
                CartId = cart.Id
            },
            reason: "Customer started checkout.",
            cancellationToken: cancellationToken);

        await auditLogService.LogAsync(
            actorUserId: userId,
            action: "InventoryReserved",
            entityName: nameof(Order),
            entityId: order.Id.ToString(),
            oldValue: null,
            newValue: new
            {
                OrderId = order.Id,
                order.OrderNumber,
                Reservations = reservedInventory
            },
            reason: "Inventory was reserved during checkout.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToCheckoutResponse(order, reservationExpiresAt);

        await SaveIdempotencyRecordAsync(
            userId,
            StartCheckoutEndpoint,
            idempotencyKey,
            StatusCodes.Status201Created,
            response,
            requestHash,
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ObjectResult(response)
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    public async Task<ActionResult<PaymentResultResponse>> ConfirmPaymentAsync(
        ConfirmPaymentRequest request,
        string? idempotencyKey,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Idempotency-Key header is required.",
                traceId));
        }

        idempotencyKey = idempotencyKey.Trim();
        var requestHash = ComputeRequestHash(ConfirmPaymentEndpoint, request);

        var previousRecord = await GetPreviousIdempotencyRecordAsync(
            userId,
            ConfirmPaymentEndpoint,
            idempotencyKey,
            cancellationToken);

        if (previousRecord is not null)
        {
            return ToStoredOrConflictResult<PaymentResultResponse>(
                previousRecord,
                requestHash,
                traceId);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        previousRecord = await GetPreviousIdempotencyRecordAsync(
            userId,
            ConfirmPaymentEndpoint,
            idempotencyKey,
            cancellationToken);

        if (previousRecord is not null)
        {
            await transaction.CommitAsync(cancellationToken);

            return ToStoredOrConflictResult<PaymentResultResponse>(
                previousRecord,
                requestHash,
                traceId);
        }

        var order = await LoadOrderForPaymentAsync(
            request.OrderId,
            userId,
            cancellationToken);

        if (order is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Order was not found.",
                traceId));
        }

        if (order.Status == OrderStatus.Paid)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Order is already paid.",
                traceId));
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Cancelled order cannot be paid.",
                traceId));
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Only pending payment orders can be paid.",
                traceId));
        }

        var now = DateTime.UtcNow;

        var invalidReservations = order.InventoryReservations
            .Where(reservation =>
                reservation.Status != InventoryReservationStatus.Pending ||
                reservation.ExpiresAt <= now)
            .Select(reservation => new
            {
                reservation.Id,
                reservation.ProductId,
                reservation.Quantity,
                Status = reservation.Status.ToString(),
                reservation.ExpiresAt,
                reservation.ConfirmedAt,
                reservation.ReleasedAt,
                reservation.ExpiredAt
            })
            .ToList();

        if (order.InventoryReservations.Count == 0 || invalidReservations.Count > 0)
        {
            var message = order.InventoryReservations.Count == 0
                ? "Order has no payable inventory reservations."
                : "Order inventory reservations are no longer payable.";

            var errorResponse = await AuditAndStoreConfirmPaymentRejectionAsync(
                userId,
                idempotencyKey,
                order,
                "PaymentConfirmationRejectedDueToInvalidReservations",
                message,
                oldValue: new
                {
                    OrderStatus = order.Status.ToString(),
                    order.OrderNumber,
                    Reservations = order.InventoryReservations.Select(reservation => new
                    {
                        reservation.Id,
                        Status = reservation.Status.ToString(),
                        reservation.ExpiresAt
                    }).ToList()
                },
                newValue: new
                {
                    RejectedAt = now,
                    Reason = message,
                    InvalidReservations = invalidReservations
                },
                requestHash: requestHash,
                now: now,
                traceId: traceId,
                cancellationToken: cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new BadRequestObjectResult(errorResponse);
        }

        if (order.CouponId.HasValue)
        {
            if (order.Coupon is null)
            {
                var message = "Order coupon was not found.";

                var errorResponse = await AuditAndStoreConfirmPaymentRejectionAsync(
                    userId,
                    idempotencyKey,
                    order,
                    "CouponRejectedDuringPaymentConfirmation",
                    message,
                    oldValue: new
                    {
                        order.CouponId,
                        order.Subtotal,
                        order.DiscountAmount
                    },
                    newValue: new
                    {
                        RejectedAt = now,
                        Reason = message
                    },
                    requestHash: requestHash,
                    now: now,
                    traceId: traceId,
                    cancellationToken: cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BadRequestObjectResult(errorResponse);
            }

            var couponValidation = await couponService.ValidateAsync(
                order.Coupon.Code,
                order.Subtotal,
                order.UserId,
                cancellationToken);

            var couponDiscountMatchesOrder = couponValidation.DiscountAmount == order.DiscountAmount;

            if (!couponValidation.IsValid ||
                couponValidation.CouponId != order.CouponId ||
                !couponDiscountMatchesOrder)
            {
                var message = !couponValidation.IsValid
                    ? couponValidation.Message
                    : "Coupon discount no longer matches the order discount.";

                var errorResponse = await AuditAndStoreConfirmPaymentRejectionAsync(
                    userId,
                    idempotencyKey,
                    order,
                    "CouponRejectedDuringPaymentConfirmation",
                    message,
                    oldValue: new
                    {
                        order.CouponId,
                        CouponCode = order.Coupon.Code,
                        order.Subtotal,
                        order.DiscountAmount
                    },
                    newValue: new
                    {
                        RejectedAt = now,
                        Reason = message,
                        ValidationCouponId = couponValidation.CouponId,
                        couponValidation.Code,
                        couponValidation.DiscountAmount,
                        couponValidation.IsValid,
                        couponValidation.Message
                    },
                    requestHash: requestHash,
                    now: now,
                    traceId: traceId,
                    cancellationToken: cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new BadRequestObjectResult(errorResponse);
            }
        }

        var payment = await GetOrCreatePaymentAsync(order, now, cancellationToken);
        var previousOrderStatus = order.Status;
        var previousPaymentStatus = payment.Status;

        if (payment.Status == PaymentStatus.Succeeded)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Payment was already confirmed.",
                traceId));
        }

        var attemptNumber = payment.Attempts.Count + 1;
        var confirmedReservations = new List<object>();

        payment.Attempts.Add(new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            AttemptNumber = attemptNumber,
            Status = PaymentAttemptStatus.Succeeded,
            CreatedAt = now
        });

        payment.Status = PaymentStatus.Succeeded;
        payment.ConfirmedAt = now;
        payment.FailedAt = null;

        order.Status = OrderStatus.Paid;
        order.PaidAt = now;

        AddStatusHistory(
            order,
            previousOrderStatus,
            order.Status,
            userId,
            "Fake payment was confirmed successfully.",
            note: null,
            createdAt: now);

        foreach (var reservation in order.InventoryReservations)
        {
            if (reservation.Status != InventoryReservationStatus.Pending)
            {
                continue;
            }

            reservation.Status = InventoryReservationStatus.Confirmed;
            reservation.ConfirmedAt = now;

            if (reservation.ProductVariantId.HasValue)
            {
                var variant = await dbContext.ProductVariants
                    .FirstOrDefaultAsync(
                        productVariant => productVariant.Id == reservation.ProductVariantId.Value,
                        cancellationToken);

                if (variant is null)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        "Product variant was not found.",
                        traceId));
                }

                if (variant.QuantityReserved < reservation.Quantity)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        "Reserved variant quantity is invalid.",
                        traceId));
                }

                var variantReservedBefore = variant.QuantityReserved;

                variant.QuantityReserved -= reservation.Quantity;
                variant.UpdatedAt = now;

                await inventoryMovementService.LogAsync(
                    productId: reservation.ProductId,
                    inventoryItemId: Guid.Empty,
                    type: InventoryMovementType.SaleConfirmed,
                    quantityChange: -reservation.Quantity,
                    quantityAvailableBefore: variant.QuantityAvailable,
                    quantityAvailableAfter: variant.QuantityAvailable,
                    quantityReservedBefore: variantReservedBefore,
                    quantityReservedAfter: variant.QuantityReserved,
                    reason: "Reserved product variant inventory was confirmed as a sale.",
                    referenceType: nameof(InventoryReservation),
                    referenceId: reservation.Id.ToString(),
                    actorUserId: userId,
                    createdAt: now,
                    cancellationToken: cancellationToken,
                    productVariantId: reservation.ProductVariantId);

                confirmedReservations.Add(new
                {
                    reservation.Id,
                    reservation.ProductId,
                    reservation.ProductVariantId,
                    VariantName = variant.Name,
                    reservation.Quantity,
                    OldStatus = InventoryReservationStatus.Pending.ToString(),
                    NewStatus = reservation.Status.ToString(),
                    reservation.ConfirmedAt,
                    QuantityReservedBefore = variantReservedBefore,
                    QuantityReservedAfter = variant.QuantityReserved
                });

                continue;
            }

            var inventoryItem = await dbContext.InventoryItems
                .FirstOrDefaultAsync(
                    item => item.ProductId == reservation.ProductId,
                    cancellationToken);

            if (inventoryItem is null)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Inventory item was not found.",
                    traceId));
            }

            if (inventoryItem.QuantityReserved < reservation.Quantity)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Reserved inventory quantity is invalid.",
                    traceId));
            }

            var quantityReservedBefore = inventoryItem.QuantityReserved;
            var quantityAvailableBefore = inventoryItem.QuantityAvailable;

            inventoryItem.QuantityReserved -= reservation.Quantity;
            inventoryItem.UpdatedAt = now;

            await inventoryMovementService.LogAsync(
                productId: inventoryItem.ProductId,
                inventoryItemId: inventoryItem.Id,
                type: InventoryMovementType.SaleConfirmed,
                quantityChange: -reservation.Quantity,
                quantityAvailableBefore: quantityAvailableBefore,
                quantityAvailableAfter: inventoryItem.QuantityAvailable,
                quantityReservedBefore: quantityReservedBefore,
                quantityReservedAfter: inventoryItem.QuantityReserved,
                reason: "Reserved inventory was confirmed as a sale.",
                referenceType: nameof(InventoryReservation),
                referenceId: reservation.Id.ToString(),
                actorUserId: userId,
                createdAt: now,
                cancellationToken: cancellationToken);

            confirmedReservations.Add(new
            {
                reservation.Id,
                reservation.ProductId,
                reservation.Quantity,
                OldStatus = InventoryReservationStatus.Pending.ToString(),
                NewStatus = reservation.Status.ToString(),
                reservation.ConfirmedAt,
                InventoryItemId = inventoryItem.Id,
                QuantityReservedBefore = quantityReservedBefore,
                QuantityReservedAfter = inventoryItem.QuantityReserved
            });
        }

        await auditLogService.LogAsync(
            actorUserId: userId,
            action: "PaymentSucceeded",
            entityName: nameof(Order),
            entityId: order.Id.ToString(),
            oldValue: new
            {
                OrderStatus = previousOrderStatus.ToString(),
                PaymentStatus = previousPaymentStatus.ToString()
            },
            newValue: new
            {
                OrderStatus = order.Status.ToString(),
                PaymentStatus = payment.Status.ToString(),
                PaymentId = payment.Id,
                payment.ProviderReference,
                payment.Amount,
                AttemptNumber = attemptNumber,
                ConfirmedReservations = confirmedReservations
            },
            reason: "Fake payment was confirmed successfully.",
            cancellationToken: cancellationToken);

        if (order.CouponId.HasValue &&
            order.CouponRedemption is null &&
            !await dbContext.CouponRedemptions.AnyAsync(
                redemption => redemption.OrderId == order.Id,
                cancellationToken))
        {
            var coupon = order.Coupon ?? await dbContext.Coupons
                .FirstOrDefaultAsync(
                    coupon => coupon.Id == order.CouponId.Value,
                    cancellationToken);

            if (coupon is null)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Order coupon was not found.",
                    traceId));
            }

            var previousUsageCount = coupon.UsageCount;

            coupon.UsageCount++;
            coupon.UpdatedAt = now;

            var redemption = new CouponRedemption
            {
                Id = Guid.NewGuid(),
                CouponId = coupon.Id,
                Coupon = coupon,
                UserId = order.UserId,
                OrderId = order.Id,
                Order = order,
                CodeSnapshot = coupon.Code,
                DiscountAmount = order.DiscountAmount,
                CreatedAt = now
            };

            dbContext.CouponRedemptions.Add(redemption);

            await auditLogService.LogAsync(
                actorUserId: userId,
                action: "CouponRedeemed",
                entityName: nameof(Coupon),
                entityId: coupon.Id.ToString(),
                oldValue: new
                {
                    UsageCount = previousUsageCount
                },
                newValue: new
                {
                    UsageCount = coupon.UsageCount,
                    RedemptionId = redemption.Id,
                    OrderId = order.Id,
                    order.OrderNumber,
                    CouponCode = coupon.Code,
                    redemption.DiscountAmount
                },
                reason: "Coupon was redeemed after successful payment.",
                cancellationToken: cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToPaymentResultResponse(order, payment, attemptNumber);

        await SaveIdempotencyRecordAsync(
            userId,
            ConfirmPaymentEndpoint,
            idempotencyKey,
            StatusCodes.Status200OK,
            response,
            requestHash,
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new OkObjectResult(response);
    }

    public async Task<ActionResult<PaymentResultResponse>> FailPaymentAsync(
        FailPaymentRequest request,
        string? idempotencyKey,
        Guid userId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Idempotency-Key header is required.",
                traceId));
        }

        idempotencyKey = idempotencyKey.Trim();
        var requestHash = ComputeRequestHash(FailPaymentEndpoint, request);

        var previousRecord = await GetPreviousIdempotencyRecordAsync(
            userId,
            FailPaymentEndpoint,
            idempotencyKey,
            cancellationToken);

        if (previousRecord is not null)
        {
            return ToStoredOrConflictResult<PaymentResultResponse>(
                previousRecord,
                requestHash,
                traceId);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        previousRecord = await GetPreviousIdempotencyRecordAsync(
            userId,
            FailPaymentEndpoint,
            idempotencyKey,
            cancellationToken);

        if (previousRecord is not null)
        {
            await transaction.CommitAsync(cancellationToken);

            return ToStoredOrConflictResult<PaymentResultResponse>(
                previousRecord,
                requestHash,
                traceId);
        }

        var order = await LoadOrderForPaymentAsync(
            request.OrderId,
            userId,
            cancellationToken);

        if (order is null)
        {
            return new NotFoundObjectResult(Error(
                StatusCodes.Status404NotFound,
                "Order was not found.",
                traceId));
        }

        if (order.Status == OrderStatus.Paid)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Paid order cannot be failed.",
                traceId));
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Cancelled order cannot fail payment.",
                traceId));
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            return new BadRequestObjectResult(Error(
                StatusCodes.Status400BadRequest,
                "Only pending payment orders can fail payment.",
                traceId));
        }

        var now = DateTime.UtcNow;

        var payment = await GetOrCreatePaymentAsync(order, now, cancellationToken);
        var previousOrderStatus = order.Status;
        var previousPaymentStatus = payment.Status;

        var attemptNumber = payment.Attempts.Count + 1;
        var releasedReservations = new List<object>();

        var failureReason = string.IsNullOrWhiteSpace(request.FailureReason)
            ? "Simulated payment failure."
            : request.FailureReason.Trim();

        payment.Attempts.Add(new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            AttemptNumber = attemptNumber,
            Status = PaymentAttemptStatus.Failed,
            FailureReason = failureReason,
            CreatedAt = now
        });

        payment.Status = PaymentStatus.Failed;
        payment.FailedAt = now;

        order.Status = OrderStatus.PaymentFailed;

        AddStatusHistory(
            order,
            previousOrderStatus,
            order.Status,
            userId,
            "Fake payment failed and inventory reservation was released.",
            failureReason,
            now);

        foreach (var reservation in order.InventoryReservations)
        {
            if (reservation.Status != InventoryReservationStatus.Pending)
            {
                continue;
            }

            reservation.Status = InventoryReservationStatus.Released;
            reservation.ReleasedAt = now;

            if (reservation.ProductVariantId.HasValue)
            {
                var variant = await dbContext.ProductVariants
                    .FirstOrDefaultAsync(
                        productVariant => productVariant.Id == reservation.ProductVariantId.Value,
                        cancellationToken);

                if (variant is null)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        "Product variant was not found.",
                        traceId));
                }

                if (variant.QuantityReserved < reservation.Quantity)
                {
                    return new BadRequestObjectResult(Error(
                        StatusCodes.Status400BadRequest,
                        "Reserved variant quantity is invalid.",
                        traceId));
                }

                var variantAvailableBefore = variant.QuantityAvailable;
                var variantReservedBefore = variant.QuantityReserved;

                variant.QuantityReserved -= reservation.Quantity;
                variant.QuantityAvailable += reservation.Quantity;
                variant.UpdatedAt = now;

                await inventoryMovementService.LogAsync(
                    productId: reservation.ProductId,
                    inventoryItemId: Guid.Empty,
                    type: InventoryMovementType.ReservationReleased,
                    quantityChange: reservation.Quantity,
                    quantityAvailableBefore: variantAvailableBefore,
                    quantityAvailableAfter: variant.QuantityAvailable,
                    quantityReservedBefore: variantReservedBefore,
                    quantityReservedAfter: variant.QuantityReserved,
                    reason: "Product variant inventory reservation was released after failed payment.",
                    referenceType: nameof(InventoryReservation),
                    referenceId: reservation.Id.ToString(),
                    actorUserId: userId,
                    createdAt: now,
                    cancellationToken: cancellationToken,
                    productVariantId: reservation.ProductVariantId);

                releasedReservations.Add(new
                {
                    reservation.Id,
                    reservation.ProductId,
                    reservation.ProductVariantId,
                    VariantName = variant.Name,
                    reservation.Quantity,
                    OldStatus = InventoryReservationStatus.Pending.ToString(),
                    NewStatus = reservation.Status.ToString(),
                    reservation.ReleasedAt,
                    QuantityAvailableBefore = variantAvailableBefore,
                    QuantityAvailableAfter = variant.QuantityAvailable,
                    QuantityReservedBefore = variantReservedBefore,
                    QuantityReservedAfter = variant.QuantityReserved
                });

                continue;
            }

            var inventoryItem = await dbContext.InventoryItems
                .FirstOrDefaultAsync(
                    item => item.ProductId == reservation.ProductId,
                    cancellationToken);

            if (inventoryItem is null)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Inventory item was not found.",
                    traceId));
            }

            if (inventoryItem.QuantityReserved < reservation.Quantity)
            {
                return new BadRequestObjectResult(Error(
                    StatusCodes.Status400BadRequest,
                    "Reserved inventory quantity is invalid.",
                    traceId));
            }

            var quantityAvailableBefore = inventoryItem.QuantityAvailable;
            var quantityReservedBefore = inventoryItem.QuantityReserved;

            inventoryItem.QuantityReserved -= reservation.Quantity;
            inventoryItem.QuantityAvailable += reservation.Quantity;
            inventoryItem.UpdatedAt = now;

            await inventoryMovementService.LogAsync(
                productId: inventoryItem.ProductId,
                inventoryItemId: inventoryItem.Id,
                type: InventoryMovementType.ReservationReleased,
                quantityChange: reservation.Quantity,
                quantityAvailableBefore: quantityAvailableBefore,
                quantityAvailableAfter: inventoryItem.QuantityAvailable,
                quantityReservedBefore: quantityReservedBefore,
                quantityReservedAfter: inventoryItem.QuantityReserved,
                reason: "Inventory reservation was released after failed payment.",
                referenceType: nameof(InventoryReservation),
                referenceId: reservation.Id.ToString(),
                actorUserId: userId,
                createdAt: now,
                cancellationToken: cancellationToken);

            releasedReservations.Add(new
            {
                reservation.Id,
                reservation.ProductId,
                reservation.Quantity,
                OldStatus = InventoryReservationStatus.Pending.ToString(),
                NewStatus = reservation.Status.ToString(),
                reservation.ReleasedAt,
                InventoryItemId = inventoryItem.Id,
                QuantityAvailableBefore = quantityAvailableBefore,
                QuantityAvailableAfter = inventoryItem.QuantityAvailable,
                QuantityReservedBefore = quantityReservedBefore,
                QuantityReservedAfter = inventoryItem.QuantityReserved
            });
        }

        await auditLogService.LogAsync(
            actorUserId: userId,
            action: "PaymentFailed",
            entityName: nameof(Order),
            entityId: order.Id.ToString(),
            oldValue: new
            {
                OrderStatus = previousOrderStatus.ToString(),
                PaymentStatus = previousPaymentStatus.ToString()
            },
            newValue: new
            {
                OrderStatus = order.Status.ToString(),
                PaymentStatus = payment.Status.ToString(),
                PaymentId = payment.Id,
                payment.ProviderReference,
                payment.Amount,
                AttemptNumber = attemptNumber,
                FailureReason = failureReason,
                ReleasedReservations = releasedReservations
            },
            reason: "Fake payment failed and inventory reservation was released.",
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToPaymentResultResponse(order, payment, attemptNumber);

        await SaveIdempotencyRecordAsync(
            userId,
            FailPaymentEndpoint,
            idempotencyKey,
            StatusCodes.Status200OK,
            response,
            requestHash,
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new OkObjectResult(response);
    }

    public string GenerateOrderNumber()
    {
        return $"MTG-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
    }

    public void ApplyShippingSnapshot(Order order, CustomerAddress address)
    {
        order.ShippingAddressId = address.Id;
        order.ShippingFullName = address.FullName;
        order.ShippingPhoneNumber = address.PhoneNumber;
        order.ShippingCountry = address.Country;
        order.ShippingCity = address.City;
        order.ShippingArea = address.Area;
        order.ShippingStreet = address.Street;
        order.ShippingBuilding = address.Building;
        order.ShippingFloor = address.Floor;
        order.ShippingApartment = address.Apartment;
        order.ShippingPostalCode = address.PostalCode;
        order.ShippingNotes = address.Notes;
    }

    public object ToShippingAuditSnapshot(Order order)
    {
        return new
        {
            order.ShippingAddressId,
            order.ShippingFullName,
            order.ShippingPhoneNumber,
            order.ShippingCountry,
            order.ShippingCity,
            order.ShippingArea,
            order.ShippingStreet,
            order.ShippingBuilding,
            order.ShippingFloor,
            order.ShippingApartment,
            order.ShippingPostalCode,
            order.ShippingNotes
        };
    }


    private void AddStatusHistory(
        Order order,
        OrderStatus? previousStatus,
        OrderStatus newStatus,
        Guid changedByUserId,
        string reason,
        string? note,
        DateTime createdAt)
    {
        dbContext.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId,
            Reason = reason,
            Note = NormalizeOptional(note),
            CreatedAt = createdAt
        });
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private ApiErrorResponse Error(int statusCode, string message, string traceId)
    {
        return new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = traceId
        };
    }

    private async Task<IdempotencyRecord?> GetPreviousIdempotencyRecordAsync(
        Guid userId,
        string endpoint,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(record =>
                record.UserId == userId &&
                record.Endpoint == endpoint &&
                record.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    private async Task SaveIdempotencyRecordAsync<TResponse>(
        Guid userId,
        string endpoint,
        string idempotencyKey,
        int statusCode,
        TResponse response,
        string requestHash,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);

        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Endpoint = endpoint,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            StatusCode = statusCode,
            ResponseJson = responseJson,
            CreatedAt = createdAt
        });

        await Task.CompletedTask;
    }

    private ActionResult<TResponse> ToStoredOrConflictResult<TResponse>(
        IdempotencyRecord record,
        string requestHash,
        string traceId)
    {
        if (!string.IsNullOrWhiteSpace(record.RequestHash) &&
            !string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return new ConflictObjectResult(Error(
                StatusCodes.Status409Conflict,
                "Idempotency-Key was already used with a different request payload.",
                traceId));
        }

        return ToStoredResult(record);
    }

    private static ContentResult ToStoredResult(IdempotencyRecord record)
    {
        return new ContentResult
        {
            Content = record.ResponseJson,
            ContentType = "application/json",
            StatusCode = record.StatusCode
        };
    }

    private static string ComputeRequestHash(string endpoint, object? request)
    {
        var payload = new
        {
            Endpoint = endpoint,
            Request = request
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(bytes);
    }

    private async Task<ShippingMethod?> LoadShippingMethodAsync(
        Guid? shippingMethodId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ShippingMethods
            .AsNoTracking()
            .Where(method => method.IsActive);

        if (shippingMethodId.HasValue)
        {
            return await query.FirstOrDefaultAsync(
                method => method.Id == shippingMethodId.Value,
                cancellationToken);
        }

        return await query
            .OrderBy(method => method.BaseCost)
            .ThenBy(method => method.EstimatedDeliveryDays)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<CustomerAddress?> LoadShippingAddressAsync(
        Guid userId,
        Guid? shippingAddressId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CustomerAddresses
            .AsNoTracking()
            .Where(address =>
                address.UserId == userId &&
                !address.IsDeleted);

        if (shippingAddressId.HasValue)
        {
            return await query.FirstOrDefaultAsync(
                address => address.Id == shippingAddressId.Value,
                cancellationToken);
        }

        return await query
            .Where(address => address.IsDefault)
            .OrderByDescending(address => address.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Order?> LoadOrderForPaymentAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .Include(order => order.Coupon)
            .Include(order => order.CouponRedemption)
            .Include(order => order.Payments)
            .ThenInclude(payment => payment.Attempts)
            .Include(order => order.InventoryReservations)
            .ThenInclude(reservation => reservation.ProductVariant)
            .FirstOrDefaultAsync(order =>
                order.Id == orderId &&
                order.UserId == userId,
                cancellationToken);
    }

    private async Task<Payment> GetOrCreatePaymentAsync(
        Order order,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var payment = order.Payments
            .OrderByDescending(payment => payment.CreatedAt)
            .FirstOrDefault();

        if (payment is not null)
        {
            return payment;
        }

        payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Amount = order.Total,
            Status = PaymentStatus.Pending,
            ProviderReference = $"FAKE-{Guid.NewGuid():N}",
            CreatedAt = now
        };

        dbContext.Payments.Add(payment);

        await Task.CompletedTask;

        return payment;
    }

    private static CheckoutStartResponse ToCheckoutResponse(
        Order order,
        DateTime reservationExpiresAt)
    {
        return new CheckoutStartResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            Subtotal = order.Subtotal,
            DiscountAmount = order.DiscountAmount,
            ShippingFee = order.ShippingFee,
            Total = order.Total,
            CreatedAt = order.CreatedAt,
            PaymentReservationExpiresAt = reservationExpiresAt,
            CouponId = order.CouponId,
            CouponCode = order.Coupon?.Code,
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
            ShippingMethodId = order.ShippingMethodId,
            ShippingMethodName = order.ShippingMethodNameSnapshot,
            ShippingMethodCode = order.ShippingMethodCodeSnapshot,
            ShippingEstimatedDeliveryDays = order.ShippingEstimatedDeliveryDays,
            ShippingStatus = order.ShippingStatus.ToString(),
            Items = order.Items
                .OrderBy(item => item.ProductNameSnapshot)
                .Select(item => new CheckoutOrderItemResponse
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductNameSnapshot,
                    ProductSku = item.ProductSkuSnapshot,
                    ProductVariantId = item.ProductVariantId,
                    VariantName = item.VariantNameSnapshot,
                    VariantSku = item.VariantSkuSnapshot,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    Total = item.Total
                })
                .ToList()
        };
    }

    private static PaymentResultResponse ToPaymentResultResponse(
        Order order,
        Payment payment,
        int attemptNumber)
    {
        return new PaymentResultResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderStatus = order.Status.ToString(),
            PaymentId = payment.Id,
            PaymentStatus = payment.Status.ToString(),
            ProviderReference = payment.ProviderReference,
            Amount = payment.Amount,
            CouponId = order.CouponId,
            CouponCode = order.Coupon?.Code,
            DiscountAmount = order.DiscountAmount,
            AttemptNumber = attemptNumber,
            CreatedAt = payment.CreatedAt,
            ConfirmedAt = payment.ConfirmedAt,
            FailedAt = payment.FailedAt
        };
    }

    private async Task<ApiErrorResponse> AuditAndStoreConfirmPaymentRejectionAsync(
        Guid userId,
        string idempotencyKey,
        Order order,
        string action,
        string message,
        object? oldValue,
        object? newValue,
        string requestHash,
        DateTime now,
        string traceId,
        CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(
            actorUserId: userId,
            action: action,
            entityName: nameof(Order),
            entityId: order.Id.ToString(),
            oldValue: oldValue,
            newValue: newValue,
            reason: message,
            cancellationToken: cancellationToken);

        var errorResponse = Error(
            StatusCodes.Status400BadRequest,
            message,
            traceId);

        await SaveIdempotencyRecordAsync(
            userId,
            ConfirmPaymentEndpoint,
            idempotencyKey,
            StatusCodes.Status400BadRequest,
            errorResponse,
            requestHash,
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return errorResponse;
    }
}
