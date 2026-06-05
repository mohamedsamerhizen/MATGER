using System.Net;
using System.Net.Http.Json;
using MATGER.Api.DTOs.Cart;
using MATGER.Api.DTOs.Checkout;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.ProductReviews;
using MATGER.Api.DTOs.Products;
using MATGER.Api.DTOs.ProductVariants;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using MATGER.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Tests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task Anonymous_user_cannot_access_customer_cart()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cart");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_access_admin_dashboard_but_admin_can()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        client.UseBearerToken(customer);
        var customerResponse = await client.GetAsync("/api/admin/dashboard/stats");

        client.UseBearerToken(admin);
        var adminResponse = await client.GetAsync("/api/admin/dashboard/stats");

        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_product_and_variant_and_duplicate_skus_are_blocked()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var category = await factory.ExecuteDbContextAsync(dbContext =>
            TestDataSeeder.CreateCategoryAsync(dbContext, "catalog"));

        client.UseBearerToken(admin);

        var productResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Test Product",
            Description = "A product created by integration tests.",
            SKU = "SKU-PRODUCT-1",
            Price = 49.99m,
            CategoryId = category.Id
        });

        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();

        var variantResponse = await client.PostAsJsonAsync($"/api/products/{product!.Id}/variants", new CreateProductVariantRequest
        {
            Name = "Large",
            SKU = "SKU-VARIANT-1",
            PriceOverride = 59.99m,
            QuantityAvailable = 5,
            LowStockThreshold = 1
        });

        var duplicateVariantSkuResponse = await client.PostAsJsonAsync($"/api/products/{product.Id}/variants", new CreateProductVariantRequest
        {
            Name = "Duplicate Large",
            SKU = "SKU-VARIANT-1",
            QuantityAvailable = 3
        });

        var productSkuCollidesWithVariantResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Colliding Product",
            Description = "Product SKU cannot reuse a variant SKU.",
            SKU = "SKU-VARIANT-1",
            Price = 40m,
            CategoryId = category.Id
        });

        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, variantResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateVariantSkuResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, productSkuCollidesWithVariantResponse.StatusCode);
    }

    [Fact]
    public async Task Public_catalog_hides_inactive_products_and_variants()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "public-catalog");
            var activeProduct = await TestDataSeeder.CreateProductAsync(dbContext, category, "ACTIVE-SKU");
            var inactiveProduct = await TestDataSeeder.CreateProductAsync(dbContext, category, "INACTIVE-SKU", isActive: false);
            var activeVariant = await TestDataSeeder.CreateVariantAsync(dbContext, activeProduct, "ACTIVE-VARIANT");
            var inactiveVariant = await TestDataSeeder.CreateVariantAsync(dbContext, activeProduct, "INACTIVE-VARIANT", isActive: false);

            return new
            {
                activeProduct.Id,
                InactiveProductId = inactiveProduct.Id,
                ActiveVariantId = activeVariant.Id,
                InactiveVariantId = inactiveVariant.Id
            };
        });

        var products = await client.GetFromJsonAsync<PaginatedResponse<ProductResponse>>("/api/products");
        var variants = await client.GetFromJsonAsync<PaginatedResponse<ProductVariantResponse>>(
            $"/api/products/{seeded.Id}/variants?includeInactive=true");
        var inactiveVariantResponse = await client.GetAsync(
            $"/api/products/{seeded.Id}/variants/{seeded.InactiveVariantId}");

        Assert.Contains(products!.Items, product => product.Id == seeded.Id);
        Assert.DoesNotContain(products.Items, product => product.Id == seeded.InactiveProductId);
        Assert.Contains(variants!.Items, variant => variant.Id == seeded.ActiveVariantId);
        Assert.DoesNotContain(variants.Items, variant => variant.Id == seeded.InactiveVariantId);
        Assert.Equal(HttpStatusCode.NotFound, inactiveVariantResponse.StatusCode);
    }

    [Fact]
    public async Task Customer_can_add_variant_to_cart_and_checkout_creates_order_payment_and_reservation_state()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "checkout");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "CHECKOUT-SKU", quantityAvailable: 0);
            var variant = await TestDataSeeder.CreateVariantAsync(dbContext, product, "CHECKOUT-VARIANT", quantityAvailable: 3);
            var address = await TestDataSeeder.CreateAddressAsync(dbContext, customer.Id);
            var shippingMethod = await TestDataSeeder.CreateShippingMethodAsync(dbContext);

            return new
            {
                ProductId = product.Id,
                VariantId = variant.Id,
                AddressId = address.Id,
                ShippingMethodId = shippingMethod.Id
            };
        });

        client.UseBearerToken(customer);

        var addToCartResponse = await client.PostAsJsonAsync("/api/cart/items", new
        {
            seeded.ProductId,
            ProductVariantId = seeded.VariantId,
            Quantity = 2
        });

        var addToCartBody = await addToCartResponse.Content.ReadAsStringAsync();
        Assert.True(
            addToCartResponse.StatusCode == HttpStatusCode.OK,
            $"Expected add-to-cart to return OK but got {(int)addToCartResponse.StatusCode}: {addToCartBody}");

        var cart = await addToCartResponse.Content.ReadFromJsonAsync<CartResponse>();

        using var startRequest = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/start")
        {
            Content = JsonContent.Create(new
            {
                ShippingAddressId = seeded.AddressId,
                ShippingMethodId = seeded.ShippingMethodId
            })
        };
        startRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var startResponse = await client.SendAsync(startRequest);
        var startBody = await startResponse.Content.ReadAsStringAsync();
        Assert.True(
            startResponse.StatusCode == HttpStatusCode.Created,
            $"Expected checkout start to return Created but got {(int)startResponse.StatusCode}: {startBody}");

        var checkout = await startResponse.Content.ReadFromJsonAsync<CheckoutStartResponse>();

        using var confirmRequest = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/confirm-payment")
        {
            Content = JsonContent.Create(new
            {
                checkout!.OrderId
            })
        };
        confirmRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var confirmResponse = await client.SendAsync(confirmRequest);
        var confirmBody = await confirmResponse.Content.ReadAsStringAsync();
        Assert.True(
            confirmResponse.StatusCode == HttpStatusCode.OK,
            $"Expected payment confirmation to return OK but got {(int)confirmResponse.StatusCode}: {confirmBody}");

        var payment = await confirmResponse.Content.ReadFromJsonAsync<PaymentResultResponse>();

        var state = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var order = await dbContext.Orders
                .Include(order => order.InventoryReservations)
                .Include(order => order.Payments)
                .FirstAsync(order => order.Id == checkout.OrderId);

            var variant = await dbContext.ProductVariants.FirstAsync(variant => variant.Id == seeded.VariantId);

            return new
            {
                order.Status,
                PaymentStatus = order.Payments.Single().Status,
                ReservationStatus = order.InventoryReservations.Single().Status,
                variant.QuantityAvailable,
                variant.QuantityReserved
            };
        });

        Assert.Equal(HttpStatusCode.OK, addToCartResponse.StatusCode);
        Assert.Equal(2, cart!.Items.Single().Quantity);
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        Assert.Equal("PendingPayment", checkout!.Status);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.Equal("Succeeded", payment!.PaymentStatus);
        Assert.Equal(OrderStatus.Paid, state.Status);
        Assert.Equal(PaymentStatus.Succeeded, state.PaymentStatus);
        Assert.Equal(InventoryReservationStatus.Confirmed, state.ReservationStatus);
        Assert.Equal(1, state.QuantityAvailable);
        Assert.Equal(0, state.QuantityReserved);
    }

    [Fact]
    public async Task Checkout_fails_on_empty_cart_and_insufficient_stock()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "stock");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "LOW-STOCK-SKU", quantityAvailable: 1);
            var address = await TestDataSeeder.CreateAddressAsync(dbContext, customer.Id);
            var shippingMethod = await TestDataSeeder.CreateShippingMethodAsync(dbContext);

            return new
            {
                ProductId = product.Id,
                AddressId = address.Id,
                ShippingMethodId = shippingMethod.Id
            };
        });

        client.UseBearerToken(customer);

        using var startRequest = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/start")
        {
            Content = JsonContent.Create(new
            {
                ShippingAddressId = seeded.AddressId,
                ShippingMethodId = seeded.ShippingMethodId
            })
        };
        startRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var emptyCheckoutResponse = await client.SendAsync(startRequest);
        var insufficientStockResponse = await client.PostAsJsonAsync("/api/cart/items", new
        {
            seeded.ProductId,
            Quantity = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, emptyCheckoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, insufficientStockResponse.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_read_another_customer_order_and_internal_notes_are_admin_only()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var owner = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var otherCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var orderId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "orders");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "ORDER-SKU");
            var order = await TestDataSeeder.CreateOrderAsync(dbContext, owner.Id, product, OrderStatus.PendingPayment);

            return order.Id;
        });

        client.UseBearerToken(otherCustomer);
        var foreignOrderResponse = await client.GetAsync($"/api/orders/{orderId}");

        client.UseBearerToken(owner);
        var customerInternalNotesResponse = await client.GetAsync($"/api/orders/{orderId}/internal-notes");

        client.UseBearerToken(admin);
        var adminInternalNoteResponse = await client.PostAsJsonAsync($"/api/orders/{orderId}/internal-notes", new
        {
            Note = "Customer asked to update delivery details."
        });

        Assert.Equal(HttpStatusCode.NotFound, foreignOrderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerInternalNotesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, adminInternalNoteResponse.StatusCode);
    }

    [Fact]
    public async Task Invalid_order_and_shipping_status_transitions_are_blocked()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var orderId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "transitions");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "TRANSITION-SKU");
            var order = await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.PendingPayment);

            return order.Id;
        });

        client.UseBearerToken(admin);

        var markShippedResponse = await client.PostAsJsonAsync($"/api/orders/{orderId}/mark-shipped", new
        {
            ShippingCarrier = "Carrier",
            TrackingNumber = "TRK-1"
        });

        var shippingStatusResponse = await client.PatchAsJsonAsync($"/api/orders/{orderId}/shipping", new
        {
            ShippingStatus = ShippingStatus.Shipped
        });

        Assert.Equal(HttpStatusCode.BadRequest, markShippedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shippingStatusResponse.StatusCode);
    }

    [Fact]
    public async Task Customer_can_request_return_only_for_own_delivered_order_and_duplicate_active_return_is_blocked()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var owner = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var otherCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);

        var orderId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "returns");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "RETURN-SKU");
            var order = await TestDataSeeder.CreateOrderAsync(dbContext, owner.Id, product, OrderStatus.Delivered);

            return order.Id;
        });

        client.UseBearerToken(otherCustomer);
        var foreignReturnResponse = await client.PostAsJsonAsync($"/api/orders/{orderId}/returns", new
        {
            Reason = "Trying to return another customer's order."
        });

        client.UseBearerToken(owner);
        var ownReturnResponse = await client.PostAsJsonAsync($"/api/orders/{orderId}/returns", new
        {
            Reason = "Item did not fit."
        });

        var duplicateReturnResponse = await client.PostAsJsonAsync($"/api/orders/{orderId}/returns", new
        {
            Reason = "Duplicate request."
        });

        Assert.Equal(HttpStatusCode.NotFound, foreignReturnResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownReturnResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateReturnResponse.StatusCode);
    }

    [Fact]
    public async Task Refund_endpoint_is_admin_only_and_double_refund_conflicts()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var ids = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "refunds");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "REFUND-SKU");
            var returnedOrder = await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Returned);
            var unpaidOrder = await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.PendingPayment);

            return new
            {
                ReturnedOrderId = returnedOrder.Id,
                UnpaidOrderId = unpaidOrder.Id
            };
        });

        client.UseBearerToken(customer);
        var customerRefundResponse = await client.PostAsync($"/api/orders/{ids.ReturnedOrderId}/refund", content: null);

        client.UseBearerToken(admin);
        var unpaidRefundResponse = await client.PostAsync($"/api/orders/{ids.UnpaidOrderId}/refund", content: null);
        var firstRefundResponse = await client.PostAsync($"/api/orders/{ids.ReturnedOrderId}/refund", content: null);
        var secondRefundResponse = await client.PostAsync($"/api/orders/{ids.ReturnedOrderId}/refund", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, customerRefundResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unpaidRefundResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstRefundResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondRefundResponse.StatusCode);
    }

    [Fact]
    public async Task Reviews_require_delivered_order_and_duplicate_review_is_blocked()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var buyer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var otherCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);

        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "reviews");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "REVIEW-SKU");

            await TestDataSeeder.CreateOrderAsync(dbContext, buyer.Id, product, OrderStatus.Delivered);

            return product.Id;
        });

        client.UseBearerToken(otherCustomer);
        var noOrderReviewResponse = await client.PostAsJsonAsync($"/api/products/{productId}/reviews", new
        {
            Rating = 5,
            Comment = "Looks good from afar."
        });

        client.UseBearerToken(buyer);
        var firstReviewResponse = await client.PostAsJsonAsync($"/api/products/{productId}/reviews", new
        {
            Rating = 5,
            Comment = "Excellent."
        });
        var review = await firstReviewResponse.Content.ReadFromJsonAsync<ProductReviewResponse>();

        var duplicateReviewResponse = await client.PostAsJsonAsync($"/api/products/{productId}/reviews", new
        {
            Rating = 4,
            Comment = "Second review."
        });

        Assert.Equal(HttpStatusCode.BadRequest, noOrderReviewResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, firstReviewResponse.StatusCode);
        Assert.Equal("Pending", review!.Status);
        Assert.Equal(HttpStatusCode.Conflict, duplicateReviewResponse.StatusCode);
    }

    [Fact]
    public async Task Inventory_and_checkout_consistency_endpoints_are_admin_only()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "inventory");
            await TestDataSeeder.CreateProductAsync(dbContext, category, "INVENTORY-SKU");
        });

        client.UseBearerToken(customer);
        var customerInventoryResponse = await client.GetAsync("/api/inventory");
        var customerConsistencyResponse = await client.GetAsync("/api/admin/checkout-consistency/summary");

        client.UseBearerToken(admin);
        var adminInventoryResponse = await client.GetAsync("/api/inventory");
        var adminConsistencyResponse = await client.GetAsync("/api/admin/checkout-consistency/summary");

        Assert.Equal(HttpStatusCode.Forbidden, customerInventoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerConsistencyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminInventoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminConsistencyResponse.StatusCode);
    }
}
