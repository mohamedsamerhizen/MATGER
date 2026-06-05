using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Coupon> Coupons => Set<Coupon>();

    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();

    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    public DbSet<OrderInternalNote> OrderInternalNotes => Set<OrderInternalNote>();

    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(user => user.IsActive)
                .IsRequired();
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(refreshToken => refreshToken.Id);

            entity.Property(refreshToken => refreshToken.TokenHash)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(refreshToken => refreshToken.CreatedAtUtc)
                .IsRequired();

            entity.Property(refreshToken => refreshToken.ExpiresAtUtc)
                .IsRequired();

            entity.Property(refreshToken => refreshToken.IsUsed)
                .IsRequired();

            entity.Property(refreshToken => refreshToken.ReplacedByTokenHash)
                .HasMaxLength(256);

            entity.HasOne(refreshToken => refreshToken.User)
                .WithMany()
                .HasForeignKey(refreshToken => refreshToken.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(refreshToken => refreshToken.TokenHash)
                .IsUnique();

            entity.HasIndex(refreshToken => refreshToken.UserId);
        });

        builder.Entity<Category>(entity =>
        {
            entity.HasKey(category => category.Id);

            entity.Property(category => category.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(category => category.Slug)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(category => category.IsActive)
                .IsRequired();

            entity.HasIndex(category => category.Slug)
                .IsUnique();
        });

        builder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);

            entity.Property(product => product.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(product => product.Description)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(product => product.SKU)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(product => product.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(product => product.IsActive)
                .IsRequired();

            entity.Property(product => product.IsFeatured)
                .IsRequired();

            entity.Property(product => product.WeightKg)
                .HasPrecision(10, 3);

            entity.Property(product => product.LengthCm)
                .HasPrecision(10, 2);

            entity.Property(product => product.WidthCm)
                .HasPrecision(10, 2);

            entity.Property(product => product.HeightCm)
                .HasPrecision(10, 2);

            entity.Property(product => product.IsReturnable)
                .IsRequired();

            entity.Property(product => product.ReturnWindowDays)
                .IsRequired();

            entity.Property(product => product.CreatedAt)
                .IsRequired();

            entity.HasOne(product => product.Category)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(product => product.SKU)
                .IsUnique();

            entity.HasIndex(product => product.CategoryId);

            entity.HasIndex(product => product.IsFeatured);
        });

        builder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(variant => variant.Id);

            entity.Property(variant => variant.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(variant => variant.SKU)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(variant => variant.PriceOverride)
                .HasPrecision(18, 2);

            entity.Property(variant => variant.IsActive)
                .IsRequired();

            entity.Property(variant => variant.QuantityAvailable)
                .IsRequired();

            entity.Property(variant => variant.QuantityReserved)
                .IsRequired();

            entity.Property(variant => variant.LowStockThreshold)
                .IsRequired();

            entity.Property(variant => variant.CreatedAt)
                .IsRequired();

            var variantRowVersion = entity.Property(variant => variant.RowVersion);

            if (isSqlite)
            {
                variantRowVersion
                    .IsConcurrencyToken(false)
                    .ValueGeneratedNever();
            }
            else
            {
                variantRowVersion.IsRowVersion();
            }

            entity.HasOne(variant => variant.Product)
                .WithMany(product => product.Variants)
                .HasForeignKey(variant => variant.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(variant => variant.SKU)
                .IsUnique();

            entity.HasIndex(variant => variant.ProductId);

            entity.HasIndex(variant => new
            {
                variant.ProductId,
                variant.IsActive
            });
        });

        builder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(inventoryItem => inventoryItem.Id);

            entity.Property(inventoryItem => inventoryItem.QuantityAvailable)
                .IsRequired();

            entity.Property(inventoryItem => inventoryItem.QuantityReserved)
                .IsRequired();

            entity.Property(inventoryItem => inventoryItem.LowStockThreshold)
                .IsRequired();

            entity.Property(inventoryItem => inventoryItem.CreatedAt)
                .IsRequired();

            var inventoryRowVersion = entity.Property(inventoryItem => inventoryItem.RowVersion);

            if (isSqlite)
            {
                inventoryRowVersion
                    .IsConcurrencyToken(false)
                    .ValueGeneratedNever();
            }
            else
            {
                inventoryRowVersion.IsRowVersion();
            }

            entity.HasOne(inventoryItem => inventoryItem.Product)
                .WithOne(product => product.InventoryItem)
                .HasForeignKey<InventoryItem>(inventoryItem => inventoryItem.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(inventoryItem => inventoryItem.ProductId)
                .IsUnique();
        });

        builder.Entity<Cart>(entity =>
        {
            entity.HasKey(cart => cart.Id);

            entity.Property(cart => cart.UserId)
                .IsRequired();

            entity.Property(cart => cart.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(cart => cart.CreatedAt)
                .IsRequired();

            entity.Property(cart => cart.ExpiresAt)
                .IsRequired();

            entity.Property(cart => cart.CouponCodeSnapshot)
                .HasMaxLength(64);

            entity.Property(cart => cart.DiscountAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.HasOne(cart => cart.User)
                .WithMany()
                .HasForeignKey(cart => cart.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cart => cart.Coupon)
                .WithMany()
                .HasForeignKey(cart => cart.CouponId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(cart => new { cart.UserId, cart.Status });

            entity.HasIndex(cart => cart.CouponId);

            entity.HasIndex(cart => cart.UserId)
                .HasFilter($"[{nameof(Cart.Status)}] = {(int)CartStatus.Active}")
                .IsUnique();
        });

        builder.Entity<CartItem>(entity =>
        {
            entity.HasKey(cartItem => cartItem.Id);

            entity.Property(cartItem => cartItem.Quantity)
                .IsRequired();

            entity.Property(cartItem => cartItem.UnitPriceSnapshot)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(cartItem => cartItem.CreatedAt)
                .IsRequired();

            entity.HasOne(cartItem => cartItem.Cart)
                .WithMany(cart => cart.Items)
                .HasForeignKey(cartItem => cartItem.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cartItem => cartItem.Product)
                .WithMany(product => product.CartItems)
                .HasForeignKey(cartItem => cartItem.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(cartItem => cartItem.ProductVariant)
                .WithMany(variant => variant.CartItems)
                .HasForeignKey(cartItem => cartItem.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(cartItem => new
            {
                cartItem.CartId,
                cartItem.ProductId,
                cartItem.ProductVariantId
            })
                .IsUnique();

            entity.HasIndex(cartItem => cartItem.ProductId);

            entity.HasIndex(cartItem => cartItem.ProductVariantId);
        });

        builder.Entity<CustomerAddress>(entity =>
        {
            entity.HasKey(address => address.Id);

            entity.Property(address => address.UserId)
                .IsRequired();

            entity.Property(address => address.Label)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(address => address.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(address => address.PhoneNumber)
                .IsRequired()
                .HasMaxLength(40);

            entity.Property(address => address.Country)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(address => address.City)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(address => address.Area)
                .HasMaxLength(120);

            entity.Property(address => address.Street)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(address => address.Building)
                .HasMaxLength(80);

            entity.Property(address => address.Floor)
                .HasMaxLength(80);

            entity.Property(address => address.Apartment)
                .HasMaxLength(80);

            entity.Property(address => address.PostalCode)
                .HasMaxLength(40);

            entity.Property(address => address.Notes)
                .HasMaxLength(500);

            entity.Property(address => address.IsDefault)
                .IsRequired();

            entity.Property(address => address.IsDeleted)
                .IsRequired();

            entity.Property(address => address.CreatedAt)
                .IsRequired();

            entity.HasOne(address => address.User)
                .WithMany()
                .HasForeignKey(address => address.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(address => address.UserId);

            entity.HasIndex(address => new
            {
                address.UserId,
                address.IsDefault
            });
        });

        builder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(movement => movement.Id);

            entity.Property(movement => movement.Type)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(movement => movement.QuantityChange)
                .IsRequired();

            entity.Property(movement => movement.QuantityAvailableBefore)
                .IsRequired();

            entity.Property(movement => movement.QuantityAvailableAfter)
                .IsRequired();

            entity.Property(movement => movement.QuantityReservedBefore)
                .IsRequired();

            entity.Property(movement => movement.QuantityReservedAfter)
                .IsRequired();

            entity.Property(movement => movement.Reason)
                .HasMaxLength(500);

            entity.Property(movement => movement.ReferenceType)
                .HasMaxLength(120);

            entity.Property(movement => movement.ReferenceId)
                .HasMaxLength(120);

            entity.Property(movement => movement.CreatedAt)
                .IsRequired();

            entity.HasOne(movement => movement.Product)
                .WithMany()
                .HasForeignKey(movement => movement.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(movement => movement.InventoryItem)
                .WithMany()
                .HasForeignKey(movement => movement.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(movement => movement.ProductVariant)
                .WithMany()
                .HasForeignKey(movement => movement.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(movement => movement.ProductId);

            entity.HasIndex(movement => movement.InventoryItemId);

            entity.HasIndex(movement => movement.ProductVariantId);

            entity.HasIndex(movement => movement.Type);

            entity.HasIndex(movement => movement.CreatedAt);

            entity.HasIndex(movement => movement.ActorUserId);
        });

        builder.Entity<Order>(entity =>
        {
            entity.HasKey(order => order.Id);

            entity.Property(order => order.OrderNumber)
                .IsRequired()
                .HasMaxLength(40);

            entity.Property(order => order.UserId)
                .IsRequired();

            entity.Property(order => order.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(order => order.Subtotal)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(order => order.DiscountAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(order => order.ShippingFee)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(order => order.Total)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(order => order.CreatedAt)
                .IsRequired();

            entity.Property(order => order.CancellationReason)
                .HasMaxLength(500);

            entity.Property(order => order.ShippingMethodNameSnapshot)
                .HasMaxLength(120);

            entity.Property(order => order.ShippingMethodCodeSnapshot)
                .HasMaxLength(60);

            entity.Property(order => order.ShippingStatus)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(order => order.ShippingCarrier)
                .HasMaxLength(120);

            entity.Property(order => order.TrackingNumber)
                .HasMaxLength(120);

            entity.Property(order => order.DeliveryNote)
                .HasMaxLength(500);

            entity.Property(order => order.ShippingFullName)
                .HasMaxLength(150);

            entity.Property(order => order.ShippingPhoneNumber)
                .HasMaxLength(40);

            entity.Property(order => order.ShippingCountry)
                .HasMaxLength(100);

            entity.Property(order => order.ShippingCity)
                .HasMaxLength(100);

            entity.Property(order => order.ShippingArea)
                .HasMaxLength(120);

            entity.Property(order => order.ShippingStreet)
                .HasMaxLength(200);

            entity.Property(order => order.ShippingBuilding)
                .HasMaxLength(80);

            entity.Property(order => order.ShippingFloor)
                .HasMaxLength(80);

            entity.Property(order => order.ShippingApartment)
                .HasMaxLength(80);

            entity.Property(order => order.ShippingPostalCode)
                .HasMaxLength(40);

            entity.Property(order => order.ShippingNotes)
                .HasMaxLength(500);

            entity.HasOne(order => order.User)
                .WithMany()
                .HasForeignKey(order => order.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(order => order.Coupon)
                .WithMany()
                .HasForeignKey(order => order.CouponId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(order => order.ShippingMethod)
                .WithMany(method => method.Orders)
                .HasForeignKey(order => order.ShippingMethodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(order => order.OrderNumber)
                .IsUnique();

            entity.HasIndex(order => order.UserId);

            entity.HasIndex(order => order.CouponId);

            entity.HasIndex(order => order.ShippingAddressId);

            entity.HasIndex(order => order.ShippingMethodId);

            entity.HasIndex(order => order.ShippingStatus);

            entity.HasIndex(order => order.Status);
        });

        builder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(orderItem => orderItem.Id);

            entity.Property(orderItem => orderItem.ProductNameSnapshot)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(orderItem => orderItem.ProductSkuSnapshot)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(orderItem => orderItem.VariantNameSnapshot)
                .HasMaxLength(150);

            entity.Property(orderItem => orderItem.VariantSkuSnapshot)
                .HasMaxLength(80);

            entity.Property(orderItem => orderItem.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(orderItem => orderItem.Quantity)
                .IsRequired();

            entity.Property(orderItem => orderItem.Total)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.HasOne(orderItem => orderItem.Order)
                .WithMany(order => order.Items)
                .HasForeignKey(orderItem => orderItem.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(orderItem => orderItem.Product)
                .WithMany()
                .HasForeignKey(orderItem => orderItem.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(orderItem => orderItem.ProductVariant)
                .WithMany(variant => variant.OrderItems)
                .HasForeignKey(orderItem => orderItem.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(orderItem => orderItem.OrderId);

            entity.HasIndex(orderItem => orderItem.ProductId);

            entity.HasIndex(orderItem => orderItem.ProductVariantId);
        });

        builder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(history => history.Id);

            entity.Property(history => history.PreviousStatus)
                .HasConversion<int?>();

            entity.Property(history => history.NewStatus)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(history => history.Reason)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(history => history.Note)
                .HasMaxLength(2000);

            entity.Property(history => history.CreatedAt)
                .IsRequired();

            entity.HasOne(history => history.Order)
                .WithMany(order => order.StatusHistories)
                .HasForeignKey(history => history.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(history => history.ChangedByUser)
                .WithMany()
                .HasForeignKey(history => history.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(history => history.OrderId);

            entity.HasIndex(history => history.NewStatus);

            entity.HasIndex(history => history.ChangedByUserId);

            entity.HasIndex(history => history.CreatedAt);
        });

        builder.Entity<OrderInternalNote>(entity =>
        {
            entity.HasKey(note => note.Id);

            entity.Property(note => note.Note)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(note => note.CreatedAt)
                .IsRequired();

            entity.HasOne(note => note.Order)
                .WithMany(order => order.InternalNotes)
                .HasForeignKey(note => note.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(note => note.AuthorUser)
                .WithMany()
                .HasForeignKey(note => note.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(note => note.OrderId);

            entity.HasIndex(note => note.AuthorUserId);

            entity.HasIndex(note => note.CreatedAt);
        });

        builder.Entity<InventoryReservation>(entity =>
        {
            entity.HasKey(reservation => reservation.Id);

            entity.Property(reservation => reservation.Quantity)
                .IsRequired();

            entity.Property(reservation => reservation.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(reservation => reservation.CreatedAt)
                .IsRequired();

            entity.Property(reservation => reservation.ExpiresAt)
                .IsRequired();

            entity.HasOne(reservation => reservation.Order)
                .WithMany(order => order.InventoryReservations)
                .HasForeignKey(reservation => reservation.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(reservation => reservation.Product)
                .WithMany()
                .HasForeignKey(reservation => reservation.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(reservation => reservation.ProductVariant)
                .WithMany(variant => variant.InventoryReservations)
                .HasForeignKey(reservation => reservation.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(reservation => reservation.OrderId);

            entity.HasIndex(reservation => reservation.ProductId);

            entity.HasIndex(reservation => reservation.ProductVariantId);

            entity.HasIndex(reservation => reservation.Status);

            entity.HasIndex(reservation => reservation.ExpiresAt);
        });

        builder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(record => record.Id);

            entity.Property(record => record.UserId)
                .IsRequired();

            entity.Property(record => record.Endpoint)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(record => record.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(record => record.RequestHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(record => record.StatusCode)
                .IsRequired();

            entity.Property(record => record.ResponseJson)
                .IsRequired();

            entity.Property(record => record.CreatedAt)
                .IsRequired();

            entity.HasOne(record => record.User)
                .WithMany()
                .HasForeignKey(record => record.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(record => new
                {
                    record.UserId,
                    record.Endpoint,
                    record.IdempotencyKey
                })
                .IsUnique();

            entity.HasIndex(record => record.CreatedAt);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.HasKey(payment => payment.Id);

            entity.Property(payment => payment.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(payment => payment.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(payment => payment.ProviderReference)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(payment => payment.CreatedAt)
                .IsRequired();

            entity.HasOne(payment => payment.Order)
                .WithMany(order => order.Payments)
                .HasForeignKey(payment => payment.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(payment => payment.OrderId);

            entity.HasIndex(payment => payment.ProviderReference)
                .IsUnique();
        });

        builder.Entity<PaymentAttempt>(entity =>
        {
            entity.HasKey(paymentAttempt => paymentAttempt.Id);

            entity.Property(paymentAttempt => paymentAttempt.AttemptNumber)
                .IsRequired();

            entity.Property(paymentAttempt => paymentAttempt.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(paymentAttempt => paymentAttempt.FailureReason)
                .HasMaxLength(500);

            entity.Property(paymentAttempt => paymentAttempt.CreatedAt)
                .IsRequired();

            entity.HasOne(paymentAttempt => paymentAttempt.Payment)
                .WithMany(payment => payment.Attempts)
                .HasForeignKey(paymentAttempt => paymentAttempt.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(paymentAttempt => paymentAttempt.PaymentId);
        });

        builder.Entity<ReturnRequest>(entity =>
        {
            entity.HasKey(returnRequest => returnRequest.Id);

            entity.Property(returnRequest => returnRequest.Reason)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(returnRequest => returnRequest.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(returnRequest => returnRequest.AdminNote)
                .HasMaxLength(500);

            entity.Property(returnRequest => returnRequest.RequestedAt)
                .IsRequired();

            entity.HasOne(returnRequest => returnRequest.Order)
                .WithMany(order => order.ReturnRequests)
                .HasForeignKey(returnRequest => returnRequest.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(returnRequest => returnRequest.User)
                .WithMany()
                .HasForeignKey(returnRequest => returnRequest.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(returnRequest => returnRequest.OrderId);

            entity.HasIndex(returnRequest => returnRequest.UserId);

            entity.HasIndex(returnRequest => returnRequest.Status);

            entity.HasIndex(returnRequest => returnRequest.RequestedAt);
        });

        builder.Entity<Refund>(entity =>
        {
            entity.HasKey(refund => refund.Id);

            entity.Property(refund => refund.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(refund => refund.Reason)
                .HasMaxLength(500);

            entity.Property(refund => refund.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(refund => refund.ProviderReference)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(refund => refund.CreatedAt)
                .IsRequired();

            entity.HasOne(refund => refund.Order)
                .WithMany(order => order.Refunds)
                .HasForeignKey(refund => refund.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(refund => refund.OrderId)
                .IsUnique();

            entity.HasIndex(refund => refund.Status);

            entity.HasIndex(refund => refund.CreatedAt);
        });


        builder.Entity<WishlistItem>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.UserId)
                .IsRequired();

            entity.Property(item => item.ProductId)
                .IsRequired();

            entity.Property(item => item.CreatedAt)
                .IsRequired();

            entity.HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Product)
                .WithMany(product => product.WishlistItems)
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(item => new
                {
                    item.UserId,
                    item.ProductId
                })
                .IsUnique();

            entity.HasIndex(item => item.ProductId);

            entity.HasIndex(item => item.CreatedAt);
        });

        builder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(review => review.Id);

            entity.Property(review => review.UserId)
                .IsRequired();

            entity.Property(review => review.ProductId)
                .IsRequired();

            entity.Property(review => review.OrderId)
                .IsRequired();

            entity.Property(review => review.Rating)
                .IsRequired();

            entity.Property(review => review.Comment)
                .HasMaxLength(1000);

            entity.Property(review => review.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(review => review.CreatedAt)
                .IsRequired();

            entity.Property(review => review.AdminNote)
                .HasMaxLength(500);

            entity.HasOne(review => review.User)
                .WithMany()
                .HasForeignKey(review => review.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(review => review.Product)
                .WithMany(product => product.Reviews)
                .HasForeignKey(review => review.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(review => review.Order)
                .WithMany()
                .HasForeignKey(review => review.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(review => review.HiddenByUser)
                .WithMany()
                .HasForeignKey(review => review.HiddenByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(review => new
                {
                    review.UserId,
                    review.ProductId
                })
                .IsUnique();

            entity.HasIndex(review => review.ProductId);

            entity.HasIndex(review => review.OrderId);

            entity.HasIndex(review => review.Status);

            entity.HasIndex(review => review.CreatedAt);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(auditLog => auditLog.Id);

            entity.Property(auditLog => auditLog.Action)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(auditLog => auditLog.EntityName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(auditLog => auditLog.EntityId)
                .IsRequired()
                .HasMaxLength(100);

            var oldValueJson = entity.Property(auditLog => auditLog.OldValueJson);
            var newValueJson = entity.Property(auditLog => auditLog.NewValueJson);

            if (!isSqlite)
            {
                oldValueJson.HasColumnType("nvarchar(max)");
                newValueJson.HasColumnType("nvarchar(max)");
            }

            entity.Property(auditLog => auditLog.Reason)
                .HasMaxLength(500);

            entity.Property(auditLog => auditLog.CreatedAt)
                .IsRequired();

            entity.HasIndex(auditLog => auditLog.ActorUserId);

            entity.HasIndex(auditLog => new
            {
                auditLog.EntityName,
                auditLog.EntityId
            });

            entity.HasIndex(auditLog => auditLog.CreatedAt);
        });

        builder.Entity<ShippingMethod>(entity =>
        {
            entity.HasKey(method => method.Id);

            entity.Property(method => method.Name)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(method => method.Code)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(method => method.BaseCost)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(method => method.EstimatedDeliveryDays)
                .IsRequired();

            entity.Property(method => method.IsActive)
                .IsRequired();

            entity.Property(method => method.CreatedAt)
                .IsRequired();

            entity.HasIndex(method => method.Code)
                .IsUnique();
        });

        builder.Entity<Coupon>(entity =>
        {
            entity.HasKey(coupon => coupon.Id);

            entity.Property(coupon => coupon.Code)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(coupon => coupon.Name)
                .IsRequired()
                .HasMaxLength(160);

            entity.Property(coupon => coupon.Description)
                .HasMaxLength(1000);

            entity.Property(coupon => coupon.DiscountType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(coupon => coupon.DiscountValue)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(coupon => coupon.MaxDiscountAmount)
                .HasPrecision(18, 2);

            entity.Property(coupon => coupon.MinimumOrderSubtotal)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(coupon => coupon.StartsAt)
                .IsRequired();

            entity.Property(coupon => coupon.IsActive)
                .IsRequired();

            entity.Property(coupon => coupon.UsageCount)
                .IsRequired();

            entity.Property(coupon => coupon.CreatedAt)
                .IsRequired();

            entity.HasIndex(coupon => coupon.Code)
                .IsUnique();
        });

        builder.Entity<CouponRedemption>(entity =>
        {
            entity.HasKey(redemption => redemption.Id);

            entity.Property(redemption => redemption.CodeSnapshot)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(redemption => redemption.DiscountAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(redemption => redemption.CreatedAt)
                .IsRequired();

            entity.HasOne(redemption => redemption.Coupon)
                .WithMany(coupon => coupon.Redemptions)
                .HasForeignKey(redemption => redemption.CouponId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(redemption => redemption.User)
                .WithMany()
                .HasForeignKey(redemption => redemption.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(redemption => redemption.Order)
                .WithOne(order => order.CouponRedemption)
                .HasForeignKey<CouponRedemption>(redemption => redemption.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(redemption => redemption.CouponId);

            entity.HasIndex(redemption => redemption.UserId);

            entity.HasIndex(redemption => redemption.OrderId)
                .IsUnique();
        });
    }
}
