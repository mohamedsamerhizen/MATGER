using MATGER.Api.DTOs.Addresses;
using System.Net;
using System.Net.Http.Json;
using MATGER.Api.DTOs.Admin;
using MATGER.Api.DTOs.Cart;
using MATGER.Api.DTOs.Checkout;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Customers;
using MATGER.Api.DTOs.Demo;
using MATGER.Api.DTOs.Fulfillment;
using MATGER.Api.DTOs.Inventory;
using MATGER.Api.DTOs.Loyalty;
using MATGER.Api.DTOs.Orders;
using MATGER.Api.DTOs.ProductReviews;
using MATGER.Api.DTOs.Products;
using MATGER.Api.DTOs.ProductVariants;
using MATGER.Api.DTOs.Risk;
using MATGER.Api.DTOs.Wallet;
using MATGER.Api.DTOs.Wishlist;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using MATGER.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        var inventoryManager = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.InventoryManager);

        client.UseBearerToken(customer);
        var customerResponse = await client.GetAsync("/api/admin/dashboard/stats");

        client.UseBearerToken(admin);
        var adminResponse = await client.GetAsync("/api/admin/dashboard/stats");

        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task Profit_report_is_admin_only_and_calculates_from_cost_snapshots()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "profit");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "PROFIT-SKU", costPrice: 10m);

            await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Paid);
        });

        var from = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var url = $"/api/admin/dashboard/profit-report?from={from}&to={to}";

        client.UseBearerToken(customer);
        var customerResponse = await client.GetAsync(url);

        client.UseBearerToken(admin);
        var adminResponse = await client.GetAsync(url);
        var report = await adminResponse.Content.ReadFromJsonAsync<AdminProfitReportResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(25m, report!.Revenue);
        Assert.Equal(10m, report.Cost);
        Assert.Equal(15m, report.GrossProfit);
        Assert.Equal(60m, report.GrossMarginPercentage);
        Assert.Contains(report.ProfitByProduct, product => product.ProductSku == "PROFIT-SKU" && product.GrossProfit == 15m);
        Assert.Contains(report.ProfitByCategory, category => category.CategoryName == "Category profit");
    }

    [Fact]
    public async Task Public_product_response_does_not_expose_cost_price()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "cost-visibility");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "COST-VISIBILITY-SKU", costPrice: 7m);

            return product.Id;
        });

        var response = await client.GetAsync($"/api/products/{productId}");
        var body = await response.Content.ReadAsStringAsync();
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(25m, product!.Price);
        Assert.DoesNotContain("costPrice", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Product_effective_price_uses_only_active_sale_window()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var now = DateTime.UtcNow;
        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "sale-windows");
            var active = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "ACTIVE-SALE-SKU",
                salePrice: 15m,
                saleStartAtUtc: now.AddDays(-1),
                saleEndAtUtc: now.AddDays(1));
            var expired = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "EXPIRED-SALE-SKU",
                salePrice: 12m,
                saleStartAtUtc: now.AddDays(-3),
                saleEndAtUtc: now.AddDays(-1));
            var upcoming = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "UPCOMING-SALE-SKU",
                salePrice: 14m,
                saleStartAtUtc: now.AddDays(1),
                saleEndAtUtc: now.AddDays(3));

            return new
            {
                ActiveId = active.Id,
                ExpiredId = expired.Id,
                UpcomingId = upcoming.Id
            };
        });

        var activeProduct = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{seeded.ActiveId}");
        var expiredProduct = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{seeded.ExpiredId}");
        var upcomingProduct = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{seeded.UpcomingId}");

        Assert.True(activeProduct!.IsSaleActive);
        Assert.Equal(15m, activeProduct.EffectivePrice);
        Assert.False(expiredProduct!.IsSaleActive);
        Assert.Equal(25m, expiredProduct.EffectivePrice);
        Assert.False(upcomingProduct!.IsSaleActive);
        Assert.Equal(25m, upcomingProduct.EffectivePrice);
    }

    [Fact]
    public async Task Checkout_uses_active_sale_price_for_order_item_snapshot()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var now = DateTime.UtcNow;
        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "sale-checkout");
            var product = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "SALE-CHECKOUT-SKU",
                salePrice: 15m,
                saleStartAtUtc: now.AddDays(-1),
                saleEndAtUtc: now.AddDays(1));
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

        var addToCartResponse = await client.PostAsJsonAsync("/api/cart/items", new
        {
            seeded.ProductId,
            Quantity = 1
        });
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
        var checkout = await startResponse.Content.ReadFromJsonAsync<CheckoutStartResponse>();

        var unitPrice = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var order = await dbContext.Orders
                .Include(order => order.Items)
                .FirstAsync(order => order.Id == checkout!.OrderId);

            return order.Items.Single().UnitPrice;
        });

        Assert.Equal(HttpStatusCode.OK, addToCartResponse.StatusCode);
        Assert.Equal(15m, cart!.Items.Single().UnitPriceSnapshot);
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        Assert.Equal(15m, unitPrice);
    }

    [Fact]
    public async Task Admin_sale_updates_create_price_history_and_customer_is_forbidden()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "sale-admin");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "SALE-ADMIN-SKU");

            return product.Id;
        });

        var now = DateTime.UtcNow;
        var saleRequest = new
        {
            SalePrice = 19m,
            SaleStartAtUtc = now.AddHours(-1),
            SaleEndAtUtc = now.AddHours(12),
            Reason = "Integration test campaign"
        };

        client.UseBearerToken(customer);
        var customerSaleResponse = await client.PutAsJsonAsync($"/api/products/{productId}/sale", saleRequest);
        var customerHistoryResponse = await client.GetAsync($"/api/products/{productId}/price-history");

        client.UseBearerToken(admin);
        var adminSaleResponse = await client.PutAsJsonAsync($"/api/products/{productId}/sale", saleRequest);
        var saleProduct = await adminSaleResponse.Content.ReadFromJsonAsync<ProductResponse>();
        var history = await client.GetFromJsonAsync<IReadOnlyList<ProductPriceHistoryResponse>>(
            $"/api/products/{productId}/price-history");

        Assert.Equal(HttpStatusCode.Forbidden, customerSaleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerHistoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminSaleResponse.StatusCode);
        Assert.Equal(19m, saleProduct!.EffectivePrice);
        Assert.Contains(history!, item => item.ChangeType == "SaleUpdated" && item.NewSalePrice == 19m);
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
    public async Task Public_product_details_return_brand_images_and_specifications()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "presentation");
            var brand = await TestDataSeeder.CreateBrandAsync(dbContext, "acme");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "PRESENTATION-SKU", brand: brand);
            var image = await TestDataSeeder.CreateProductImageAsync(dbContext, product);
            var specification = await TestDataSeeder.CreateProductSpecificationAsync(dbContext, product);

            return new
            {
                product.Id,
                BrandName = brand.Name,
                ImageId = image.Id,
                SpecificationId = specification.Id
            };
        });

        var response = await client.GetAsync($"/api/products/{seeded.Id}");
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(seeded.BrandName, product!.BrandName);
        Assert.Contains(product.Images, image => image.Id == seeded.ImageId && image.IsPrimary);
        Assert.Contains(product.Specifications, specification => specification.Id == seeded.SpecificationId);
    }

    [Fact]
    public async Task Admin_can_manage_product_images_and_specifications()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "admin-presentation");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "ADMIN-PRESENTATION-SKU");

            return product.Id;
        });

        client.UseBearerToken(admin);

        var imageResponse = await client.PostAsJsonAsync($"/api/products/{productId}/images", new
        {
            ImageUrl = "https://cdn.matger.local/products/admin-presentation.jpg",
            AltText = "Admin presentation image",
            IsPrimary = true,
            SortOrder = 1
        });

        var specificationResponse = await client.PostAsJsonAsync($"/api/products/{productId}/specifications", new
        {
            Name = "Warranty",
            Value = "12 months",
            GroupName = "Commercial",
            SortOrder = 1
        });

        var productResponse = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.Created, imageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, specificationResponse.StatusCode);
        Assert.Contains(productResponse!.Images, image => image.AltText == "Admin presentation image");
        Assert.Contains(productResponse.Specifications, specification => specification.Name == "Warranty");
    }

    [Fact]
    public async Task Customer_cannot_manage_product_images_or_specifications()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "customer-presentation");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "CUSTOMER-PRESENTATION-SKU");

            return product.Id;
        });

        client.UseBearerToken(customer);

        var imageResponse = await client.PostAsJsonAsync($"/api/products/{productId}/images", new
        {
            ImageUrl = "https://cdn.matger.local/products/customer-presentation.jpg",
            IsPrimary = true,
            SortOrder = 1
        });

        var specificationResponse = await client.PostAsJsonAsync($"/api/products/{productId}/specifications", new
        {
            Name = "Warranty",
            Value = "12 months",
            SortOrder = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, imageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, specificationResponse.StatusCode);
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
                .Include(order => order.Items)
                .Include(order => order.InventoryReservations)
                .Include(order => order.Payments)
                .FirstAsync(order => order.Id == checkout.OrderId);

            var variant = await dbContext.ProductVariants.FirstAsync(variant => variant.Id == seeded.VariantId);

            return new
            {
                order.Status,
                PaymentStatus = order.Payments.Single().Status,
                order.Items.Single().CostPriceSnapshot,
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
        Assert.Equal(10m, state.CostPriceSnapshot);
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

    [Fact]
    public async Task Reorder_needed_includes_low_stock_excludes_healthy_and_marks_critical()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var inventoryManager = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.InventoryManager);

        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "reorder");
            var low = await TestDataSeeder.CreateProductAsync(dbContext, category, "REORDER-LOW-SKU", quantityAvailable: 2);
            var critical = await TestDataSeeder.CreateProductAsync(dbContext, category, "REORDER-CRITICAL-SKU", quantityAvailable: 0);
            var healthy = await TestDataSeeder.CreateProductAsync(dbContext, category, "REORDER-HEALTHY-SKU", quantityAvailable: 25);

            low.InventoryItem!.SupplierName = "Supplier Low";
            low.InventoryItem.ReorderPoint = 5;
            low.InventoryItem.ReorderQuantity = 30;

            critical.InventoryItem!.SupplierName = "Supplier Critical";
            critical.InventoryItem.ReorderPoint = 10;
            critical.InventoryItem.ReorderQuantity = 50;

            healthy.InventoryItem!.SupplierName = "Supplier Healthy";
            healthy.InventoryItem.ReorderPoint = 5;
            healthy.InventoryItem.ReorderQuantity = 20;

            await dbContext.SaveChangesAsync();

            return new
            {
                LowId = low.Id,
                CriticalId = critical.Id,
                HealthyId = healthy.Id
            };
        });

        client.UseBearerToken(customer);
        var customerResponse = await client.GetAsync("/api/admin/inventory/reorder-needed");

        client.UseBearerToken(inventoryManager);
        var inventoryManagerResponse = await client.GetAsync("/api/admin/inventory/reorder-needed");

        client.UseBearerToken(admin);
        var adminResponse = await client.GetAsync("/api/admin/inventory/reorder-needed");
        var items = await adminResponse.Content.ReadFromJsonAsync<IReadOnlyList<ReorderNeededResponse>>();

        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inventoryManagerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Contains(items!, item => item.ProductId == seeded.LowId);
        Assert.Contains(items!, item => item.ProductId == seeded.CriticalId && item.Severity == "Critical");
        Assert.DoesNotContain(items!, item => item.ProductId == seeded.HealthyId);
    }

    [Fact]
    public async Task Customer_profile_returns_metrics_segment_and_is_admin_only()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "customer-profile");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "CUSTOMER-PROFILE-SKU");
            var orders = new List<Order>();

            for (var index = 0; index < 5; index++)
            {
                orders.Add(await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Delivered));
            }

            dbContext.WishlistItems.Add(new WishlistItem
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                ProductId = product.Id,
                CreatedAt = DateTime.UtcNow
            });

            dbContext.ProductReviews.Add(new ProductReview
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                ProductId = product.Id,
                OrderId = orders[0].Id,
                Rating = 5,
                Comment = "Useful test review.",
                Status = ProductReviewStatus.Visible,
                CreatedAt = DateTime.UtcNow
            });

            dbContext.Carts.Add(new Cart
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                Status = CartStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(3),
                Items =
                [
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Quantity = 2,
                        UnitPriceSnapshot = 25m,
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            });

            await dbContext.SaveChangesAsync();
        });

        client.UseBearerToken(customer);
        var customerResponse = await client.GetAsync($"/api/admin/customers/{customer.Id}/profile");

        client.UseBearerToken(admin);
        var adminResponse = await client.GetAsync($"/api/admin/customers/{customer.Id}/profile");
        var profile = await adminResponse.Content.ReadFromJsonAsync<CustomerProfileResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(customer.Id, profile!.UserId);
        Assert.Equal(5, profile.OrdersCount);
        Assert.Equal(125m, profile.TotalSpent);
        Assert.Equal(25m, profile.AverageOrderValue);
        Assert.Equal(1, profile.WishlistCount);
        Assert.Equal(1, profile.ReviewsCount);
        Assert.Equal(1, profile.ActiveCart.ItemsCount);
        Assert.Equal(2, profile.ActiveCart.TotalQuantity);
        Assert.Equal("VIP", profile.CustomerSegment);
        Assert.Equal("Low", profile.RiskLevel);
    }

    [Fact]
    public async Task Admin_can_manage_customer_internal_notes_and_customer_cannot_access_them()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var noteRequest = new CreateCustomerInternalNoteRequest
        {
            Note = "Important internal delivery preference.",
            IsImportant = true
        };

        client.UseBearerToken(customer);
        var customerListResponse = await client.GetAsync($"/api/admin/customers/{customer.Id}/notes");
        var customerCreateResponse = await client.PostAsJsonAsync($"/api/admin/customers/{customer.Id}/notes", noteRequest);

        client.UseBearerToken(admin);
        var createResponse = await client.PostAsJsonAsync($"/api/admin/customers/{customer.Id}/notes", noteRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerInternalNoteResponse>();
        var listResponse = await client.GetAsync($"/api/admin/customers/{customer.Id}/notes");
        var notes = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<CustomerInternalNoteResponse>>();
        var deleteResponse = await client.DeleteAsync($"/api/admin/customers/{customer.Id}/notes/{created!.Id}");
        var listAfterDeleteResponse = await client.GetAsync($"/api/admin/customers/{customer.Id}/notes");
        var notesAfterDelete = await listAfterDeleteResponse.Content.ReadFromJsonAsync<IReadOnlyList<CustomerInternalNoteResponse>>();

        Assert.Equal(HttpStatusCode.Forbidden, customerListResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerCreateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.True(created.IsImportant);
        Assert.Equal("Important internal delivery preference.", created.Note);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(notes!, note => note.Id == created.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listAfterDeleteResponse.StatusCode);
        Assert.DoesNotContain(notesAfterDelete!, note => note.Id == created.Id);
    }

    [Fact]
    public async Task High_value_new_customer_checkout_creates_risk_signal_and_admin_can_resolve()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var addressId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "risk-high-value");
            var product = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "RISK-HIGH-VALUE-SKU",
                quantityAvailable: 10,
                costPrice: 300000m);
            product.Price = 600000m;
            product.InventoryItem!.QuantityAvailable = 10;

            var address = await TestDataSeeder.CreateAddressAsync(dbContext, customer.Id);

            dbContext.Carts.Add(new Cart
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                Status = CartStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(3),
                Items =
                [
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitPriceSnapshot = product.Price,
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            });

            await dbContext.SaveChangesAsync();
            return address.Id;
        });

        client.UseBearerToken(customer);
        var checkoutResponse = await StartCheckoutAsync(client, addressId);
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutStartResponse>();
        var customerRiskResponse = await client.GetAsync("/api/admin/risk-signals/open");

        client.UseBearerToken(admin);
        var openResponse = await client.GetAsync("/api/admin/risk-signals/open");
        var openSignals = await openResponse.Content.ReadFromJsonAsync<IReadOnlyList<RiskSignalResponse>>();
        var byOrderResponse = await client.GetAsync($"/api/admin/risk-signals/orders/{checkout!.OrderId}");
        var byOrder = await byOrderResponse.Content.ReadFromJsonAsync<IReadOnlyList<RiskSignalResponse>>();
        var byCustomerResponse = await client.GetAsync($"/api/admin/risk-signals/customers/{customer.Id}");
        var byCustomer = await byCustomerResponse.Content.ReadFromJsonAsync<IReadOnlyList<RiskSignalResponse>>();
        var summaryResponse = await client.GetAsync("/api/admin/risk-signals/summary");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<RiskSummaryResponse>();

        var signal = Assert.Single(byOrder!, item => item.SignalType == "NewCustomerHighValueOrder");
        var resolveResponse = await client.PostAsJsonAsync(
            $"/api/admin/risk-signals/{signal.Id}/resolve",
            new ReviewRiskSignalRequest { ResolutionNote = "Verified by admin." });
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<RiskSignalResponse>();

        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerRiskResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openResponse.StatusCode);
        Assert.Contains(openSignals!, item => item.Id == signal.Id);
        Assert.Equal(HttpStatusCode.OK, byOrderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byCustomerResponse.StatusCode);
        Assert.Contains(byCustomer!, item => item.Id == signal.Id);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        Assert.True(summary!.OpenSignals >= 1);
        Assert.True(summary.HighOpenSignals >= 1);
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        Assert.Equal("Resolved", resolved!.Status);
        Assert.Equal("Verified by admin.", resolved.ResolutionNote);
    }

    [Fact]
    public async Task Normal_checkout_does_not_create_risk_signal()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var addressId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "risk-normal");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "RISK-NORMAL-SKU");
            var address = await TestDataSeeder.CreateAddressAsync(dbContext, customer.Id);

            dbContext.Carts.Add(new Cart
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                Status = CartStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(3),
                Items =
                [
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitPriceSnapshot = product.Price,
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            });

            await dbContext.SaveChangesAsync();
            return address.Id;
        });

        client.UseBearerToken(customer);
        var checkoutResponse = await StartCheckoutAsync(client, addressId);
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutStartResponse>();

        client.UseBearerToken(admin);
        var byOrderResponse = await client.GetAsync($"/api/admin/risk-signals/orders/{checkout!.OrderId}");
        var byOrder = await byOrderResponse.Content.ReadFromJsonAsync<IReadOnlyList<RiskSignalResponse>>();

        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byOrderResponse.StatusCode);
        Assert.Empty(byOrder!);
    }

    [Fact]
    public async Task Wallet_auto_creates_admin_adjusts_records_transactions_and_blocks_negative_balance()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        client.UseBearerToken(customer);
        var initialWalletResponse = await client.GetAsync("/api/wallet");
        var initialWallet = await initialWalletResponse.Content.ReadFromJsonAsync<CustomerWalletResponse>();
        var forbiddenCreditResponse = await client.PostAsJsonAsync(
            $"/api/admin/customers/{customer.Id}/wallet/credit",
            new WalletAdjustmentRequest
            {
                Amount = 100m,
                Note = "Customer should not be able to credit wallet."
            });

        client.UseBearerToken(admin);
        var creditResponse = await client.PostAsJsonAsync(
            $"/api/admin/customers/{customer.Id}/wallet/credit",
            new WalletAdjustmentRequest
            {
                Amount = 100m,
                Note = "Support credit."
            });
        var credited = await creditResponse.Content.ReadFromJsonAsync<CustomerWalletResponse>();
        var debitResponse = await client.PostAsJsonAsync(
            $"/api/admin/customers/{customer.Id}/wallet/debit",
            new WalletAdjustmentRequest
            {
                Amount = 40m,
                Note = "Support debit."
            });
        var debited = await debitResponse.Content.ReadFromJsonAsync<CustomerWalletResponse>();
        var negativeDebitResponse = await client.PostAsJsonAsync(
            $"/api/admin/customers/{customer.Id}/wallet/debit",
            new WalletAdjustmentRequest
            {
                Amount = 1000m,
                Note = "Should fail."
            });

        client.UseBearerToken(customer);
        var walletResponse = await client.GetAsync("/api/wallet");
        var wallet = await walletResponse.Content.ReadFromJsonAsync<CustomerWalletResponse>();
        var transactionsResponse = await client.GetAsync("/api/wallet/transactions");
        var transactions = await transactionsResponse.Content.ReadFromJsonAsync<IReadOnlyList<CustomerWalletTransactionResponse>>();

        Assert.Equal(HttpStatusCode.OK, initialWalletResponse.StatusCode);
        Assert.Equal(0m, initialWallet!.Balance);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreditResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, creditResponse.StatusCode);
        Assert.Equal(100m, credited!.Balance);
        Assert.Equal(HttpStatusCode.OK, debitResponse.StatusCode);
        Assert.Equal(60m, debited!.Balance);
        Assert.Equal(HttpStatusCode.BadRequest, negativeDebitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, walletResponse.StatusCode);
        Assert.Equal(60m, wallet!.Balance);
        Assert.Equal(HttpStatusCode.OK, transactionsResponse.StatusCode);
        Assert.Contains(transactions!, transaction => transaction.Type == "Credit" && transaction.Amount == 100m);
        Assert.Contains(transactions!, transaction => transaction.Type == "Debit" && transaction.Amount == 40m);
    }

    [Fact]
    public async Task Loyalty_awards_points_on_delivered_order_and_admin_adjust_blocks_negative_balance()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var cancelledCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var orderManager = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.OrderManager);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "loyalty");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "LOYALTY-SKU");
            var shippedOrder = await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Shipped);
            await TestDataSeeder.CreateOrderAsync(dbContext, cancelledCustomer.Id, product, OrderStatus.Cancelled);

            return new
            {
                ShippedOrderId = shippedOrder.Id
            };
        });

        client.UseBearerToken(orderManager);
        var deliveredResponse = await client.PostAsJsonAsync(
            $"/api/orders/{seeded.ShippedOrderId}/mark-delivered",
            new DeliverOrderRequest { DeliveryNote = "Delivered for loyalty test." });

        client.UseBearerToken(customer);
        var loyaltyResponse = await client.GetAsync("/api/loyalty");
        var loyalty = await loyaltyResponse.Content.ReadFromJsonAsync<LoyaltyAccountResponse>();
        var transactionsResponse = await client.GetAsync("/api/loyalty/transactions");
        var transactions = await transactionsResponse.Content.ReadFromJsonAsync<IReadOnlyList<LoyaltyTransactionResponse>>();
        var forbiddenAdjustResponse = await client.PostAsJsonAsync(
            $"/api/admin/customers/{customer.Id}/loyalty/adjust",
            new AdjustLoyaltyPointsRequest
            {
                Points = 10,
                Note = "Customer should not adjust loyalty."
            });

        client.UseBearerToken(cancelledCustomer);
        var cancelledLoyaltyResponse = await client.GetAsync("/api/loyalty");
        var cancelledLoyalty = await cancelledLoyaltyResponse.Content.ReadFromJsonAsync<LoyaltyAccountResponse>();

        client.UseBearerToken(admin);
        var adjustResponse = await client.PostAsJsonAsync(
            $"/api/admin/customers/{customer.Id}/loyalty/adjust",
            new AdjustLoyaltyPointsRequest
            {
                Points = 10,
                Note = "Support adjustment."
            });
        var adjusted = await adjustResponse.Content.ReadFromJsonAsync<LoyaltyAccountResponse>();
        var negativeAdjustResponse = await client.PostAsJsonAsync(
            $"/api/admin/customers/{customer.Id}/loyalty/adjust",
            new AdjustLoyaltyPointsRequest
            {
                Points = -1000,
                Note = "Should fail."
            });
        var summaryResponse = await client.GetAsync("/api/admin/loyalty/summary");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<LoyaltySummaryResponse>();

        Assert.Equal(HttpStatusCode.OK, deliveredResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loyaltyResponse.StatusCode);
        Assert.True(loyalty!.PointsBalance > 0);
        Assert.True(loyalty.LifetimeEarned > 0);
        Assert.Equal(HttpStatusCode.OK, transactionsResponse.StatusCode);
        Assert.Contains(transactions!, transaction =>
            transaction.Type == "Earned" &&
            transaction.ReferenceId == seeded.ShippedOrderId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAdjustResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelledLoyaltyResponse.StatusCode);
        Assert.Equal(0, cancelledLoyalty!.PointsBalance);
        Assert.Equal(HttpStatusCode.OK, adjustResponse.StatusCode);
        Assert.Equal(loyalty.PointsBalance + 10, adjusted!.PointsBalance);
        Assert.Equal(HttpStatusCode.BadRequest, negativeAdjustResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        Assert.True(summary!.Accounts >= 2);
        Assert.True(summary.Transactions >= 2);
    }

    [Fact]
    public async Task Inventory_manager_can_create_stock_adjustment_request_and_customer_is_forbidden()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var inventoryManager = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.InventoryManager);

        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "stock-adjust-create");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "STOCK-ADJUST-CREATE-SKU");

            return product.Id;
        });

        var request = new CreateStockAdjustmentRequest
        {
            ProductId = productId,
            QuantityChange = 3,
            Reason = "Cycle count found extra units."
        };

        client.UseBearerToken(customer);
        var customerResponse = await client.PostAsJsonAsync("/api/admin/inventory/stock-adjustments", request);

        client.UseBearerToken(inventoryManager);
        var createResponse = await client.PostAsJsonAsync("/api/admin/inventory/stock-adjustments", request);
        var created = await createResponse.Content.ReadFromJsonAsync<StockAdjustmentRequestResponse>();
        var myRequestsResponse = await client.GetAsync("/api/admin/inventory/stock-adjustments/my");
        var myRequests = await myRequestsResponse.Content.ReadFromJsonAsync<IReadOnlyList<StockAdjustmentRequestResponse>>();

        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("Pending", created!.Status);
        Assert.Equal(HttpStatusCode.OK, myRequestsResponse.StatusCode);
        Assert.Contains(myRequests!, item => item.Id == created.Id);
    }

    [Fact]
    public async Task Admin_approval_updates_stock_creates_movement_and_blocks_double_approve()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var inventoryManager = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.InventoryManager);

        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "stock-adjust-approve");
            var product = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "STOCK-ADJUST-APPROVE-SKU",
                quantityAvailable: 5);

            return product.Id;
        });

        client.UseBearerToken(inventoryManager);
        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/inventory/stock-adjustments",
            new CreateStockAdjustmentRequest
            {
                ProductId = productId,
                QuantityChange = 4,
                Reason = "Warehouse recount approved."
            });
        var created = await createResponse.Content.ReadFromJsonAsync<StockAdjustmentRequestResponse>();

        client.UseBearerToken(admin);
        var approveResponse = await client.PostAsJsonAsync(
            $"/api/admin/inventory/stock-adjustments/{created!.Id}/approve",
            new ReviewStockAdjustmentRequest { ReviewNote = "Approved after recount." });
        var approved = await approveResponse.Content.ReadFromJsonAsync<StockAdjustmentRequestResponse>();
        var doubleApproveResponse = await client.PostAsJsonAsync(
            $"/api/admin/inventory/stock-adjustments/{created.Id}/approve",
            new ReviewStockAdjustmentRequest { ReviewNote = "Duplicate approval attempt." });

        var persisted = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var product = await TestDataSeeder.ReloadProductAsync(dbContext, productId);
            var adjustment = await dbContext.StockAdjustmentRequests
                .FirstAsync(request => request.Id == created.Id);
            var movementExists = adjustment.AppliedInventoryMovementId.HasValue &&
                await dbContext.InventoryMovements.AnyAsync(movement =>
                    movement.Id == adjustment.AppliedInventoryMovementId.Value &&
                    movement.QuantityChange == 4 &&
                    movement.QuantityAvailableBefore == 5 &&
                    movement.QuantityAvailableAfter == 9);

            return new
            {
                product.InventoryItem!.QuantityAvailable,
                adjustment.AppliedInventoryMovementId,
                MovementExists = movementExists
            };
        });

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal(9, persisted.QuantityAvailable);
        Assert.NotNull(persisted.AppliedInventoryMovementId);
        Assert.True(persisted.MovementExists);
        Assert.Equal(HttpStatusCode.Conflict, doubleApproveResponse.StatusCode);
    }

    [Fact]
    public async Task Reject_does_not_change_stock_and_negative_adjustment_is_blocked()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var inventoryManager = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.InventoryManager);

        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "stock-adjust-reject");
            var product = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "STOCK-ADJUST-REJECT-SKU",
                quantityAvailable: 2);

            return product.Id;
        });

        client.UseBearerToken(inventoryManager);
        var negativeCreateResponse = await client.PostAsJsonAsync(
            "/api/admin/inventory/stock-adjustments",
            new CreateStockAdjustmentRequest
            {
                ProductId = productId,
                QuantityChange = -5,
                Reason = "Shrinkage request beyond stock."
            });
        var negativeRequest = await negativeCreateResponse.Content.ReadFromJsonAsync<StockAdjustmentRequestResponse>();

        var rejectCreateResponse = await client.PostAsJsonAsync(
            "/api/admin/inventory/stock-adjustments",
            new CreateStockAdjustmentRequest
            {
                ProductId = productId,
                QuantityChange = 6,
                Reason = "Unverified receiving request."
            });
        var rejectRequest = await rejectCreateResponse.Content.ReadFromJsonAsync<StockAdjustmentRequestResponse>();

        client.UseBearerToken(admin);
        var negativeApproveResponse = await client.PostAsJsonAsync(
            $"/api/admin/inventory/stock-adjustments/{negativeRequest!.Id}/approve",
            new ReviewStockAdjustmentRequest { ReviewNote = "Would make stock negative." });
        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/admin/inventory/stock-adjustments/{rejectRequest!.Id}/reject",
            new ReviewStockAdjustmentRequest { ReviewNote = "Rejected after recount." });
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<StockAdjustmentRequestResponse>();

        var persisted = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var product = await TestDataSeeder.ReloadProductAsync(dbContext, productId);
            var rejectedAdjustment = await dbContext.StockAdjustmentRequests
                .FirstAsync(request => request.Id == rejectRequest.Id);

            return new
            {
                product.InventoryItem!.QuantityAvailable,
                rejectedAdjustment.AppliedInventoryMovementId
            };
        });

        Assert.Equal(HttpStatusCode.BadRequest, negativeApproveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        Assert.Equal("Rejected", rejected!.Status);
        Assert.Equal(2, persisted.QuantityAvailable);
        Assert.Null(persisted.AppliedInventoryMovementId);
    }

    [Fact]
    public async Task Picking_list_is_available_to_admin_and_order_manager_but_not_customer()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var orderManager = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.OrderManager);

        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "picking");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "PICKING-SKU");

            await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Paid);
            await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Processing);
            await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Delivered);
            await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Cancelled);

            return product.Id;
        });

        client.UseBearerToken(customer);
        var customerResponse = await client.GetAsync("/api/admin/fulfillment/picking-list");

        client.UseBearerToken(orderManager);
        var orderManagerResponse = await client.GetAsync("/api/admin/fulfillment/picking-list");

        client.UseBearerToken(admin);
        var adminResponse = await client.GetAsync("/api/admin/fulfillment/picking-list");
        var pickingList = await adminResponse.Content.ReadFromJsonAsync<IReadOnlyList<PickingListItemResponse>>();

        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, orderManagerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var item = Assert.Single(pickingList!, item => item.ProductId == productId);
        Assert.Equal(2, item.TotalQuantityToPick);
        Assert.Equal(2, item.NumberOfOrders);
    }

    [Fact]
    public async Task Order_csv_export_has_text_csv_headers_and_status_filter()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var orders = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "order-export");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "ORDER-EXPORT-SKU");
            var paidOrder = await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Paid);
            var cancelledOrder = await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Cancelled);

            return new
            {
                PaidOrderId = paidOrder.Id,
                CancelledOrderId = cancelledOrder.Id
            };
        });

        client.UseBearerToken(admin);

        var response = await client.GetAsync("/api/admin/exports/orders.csv?status=Paid");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("OrderId,CustomerEmail,Status,PaymentStatus,Subtotal,Discount,Shipping,Total,ItemsCount,CreatedAt,PaidAt,ShippedAt,DeliveredAt", csv);
        Assert.Contains(orders.PaidOrderId.ToString(), csv);
        Assert.DoesNotContain(orders.CancelledOrderId.ToString(), csv);
    }

    [Fact]
    public async Task Operations_dashboard_endpoints_are_admin_only_and_return_operational_values()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "operations-dashboard");
            var lowStockProduct = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "OPS-LOW-STOCK-SKU",
                quantityAvailable: 1,
                costPrice: 8m);
            var criticalStockProduct = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "OPS-CRITICAL-STOCK-SKU",
                quantityAvailable: 0,
                costPrice: 9m);
            var healthyProduct = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "OPS-HEALTHY-STOCK-SKU",
                quantityAvailable: 20,
                costPrice: 6m);

            lowStockProduct.InventoryItem!.QuantityReserved = 3;
            criticalStockProduct.InventoryItem!.QuantityReserved = 0;
            healthyProduct.InventoryItem!.QuantityReserved = 4;

            var pendingOrder = await TestDataSeeder.CreateOrderAsync(
                dbContext,
                customer.Id,
                lowStockProduct,
                OrderStatus.PendingPayment);
            var paidOrder = await TestDataSeeder.CreateOrderAsync(
                dbContext,
                customer.Id,
                healthyProduct,
                OrderStatus.Paid);
            await TestDataSeeder.CreateOrderAsync(
                dbContext,
                customer.Id,
                healthyProduct,
                OrderStatus.Processing);

            dbContext.ReturnRequests.Add(new ReturnRequest
            {
                Id = Guid.NewGuid(),
                OrderId = paidOrder.Id,
                Order = paidOrder,
                UserId = customer.Id,
                Reason = "Dashboard pending return.",
                Status = ReturnRequestStatus.Requested,
                RequestedAt = DateTime.UtcNow
            });

            dbContext.Refunds.AddRange(
                new Refund
                {
                    Id = Guid.NewGuid(),
                    OrderId = paidOrder.Id,
                    Order = paidOrder,
                    Amount = 5m,
                    Status = RefundStatus.Pending,
                    Reason = "Pending dashboard refund.",
                    ProviderReference = "PENDING-DASHBOARD-REFUND",
                    CreatedAt = DateTime.UtcNow
                },
                new Refund
                {
                    Id = Guid.NewGuid(),
                    OrderId = paidOrder.Id,
                    Order = paidOrder,
                    Amount = 5m,
                    Status = RefundStatus.Completed,
                    Reason = "Completed dashboard refund.",
                    ProviderReference = "COMPLETED-DASHBOARD-REFUND",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });

            dbContext.RiskSignals.Add(new RiskSignal
            {
                Id = Guid.NewGuid(),
                OrderId = pendingOrder.Id,
                Order = pendingOrder,
                UserId = customer.Id,
                SignalType = "DashboardOpenRiskSignal",
                Severity = RiskSignalSeverity.High,
                Details = "Open risk signal for operations dashboard.",
                CreatedAtUtc = DateTime.UtcNow,
                Status = RiskSignalStatus.Open
            });

            dbContext.StockAdjustmentRequests.Add(new StockAdjustmentRequest
            {
                Id = Guid.NewGuid(),
                ProductId = lowStockProduct.Id,
                Product = lowStockProduct,
                RequestedByUserId = admin.Id,
                QuantityChange = 5,
                Reason = "Dashboard pending stock adjustment.",
                Status = StockAdjustmentRequestStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        });

        client.UseBearerToken(customer);
        var customerForbiddenResponse = await client.GetAsync("/api/admin/dashboard/operations-summary");

        client.UseBearerToken(admin);
        var operationsResponse = await client.GetAsync("/api/admin/dashboard/operations-summary");
        var salesResponse = await client.GetAsync("/api/admin/dashboard/sales-overview");
        var inventoryResponse = await client.GetAsync("/api/admin/dashboard/inventory-overview");

        var operations = await operationsResponse.Content.ReadFromJsonAsync<AdminOperationsSummaryResponse>();
        var sales = await salesResponse.Content.ReadFromJsonAsync<AdminSalesOverviewResponse>();
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<AdminInventoryOverviewResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, customerForbiddenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, operationsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, salesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inventoryResponse.StatusCode);

        Assert.Equal(1, operations!.PendingOrders);
        Assert.Equal(1, operations.PaidAwaitingProcessingOrders);
        Assert.Equal(1, operations.ProcessingOrders);
        Assert.Equal(2, operations.LowStockCount);
        Assert.Equal(1, operations.CriticalStockCount);
        Assert.Equal(1, operations.PendingReturns);
        Assert.Equal(1, operations.PendingRefunds);
        Assert.Equal(1, operations.OpenRiskSignals);
        Assert.Equal(1, operations.PendingStockAdjustmentRequests);

        Assert.Equal(50m, sales!.SalesToday);
        Assert.Equal(2, sales.OrdersToday);
        Assert.Equal(50m, sales.SalesLast30Days);
        Assert.Equal(2, sales.OrdersLast30Days);
        Assert.Equal(25m, sales.AverageOrderValueLast30Days);
        Assert.Equal(5m, sales.RefundAmountLast30Days);
        Assert.Equal(10m, sales.RefundRateLast30Days);

        Assert.Equal(3, inventory!.TotalInventoryItems);
        Assert.Equal(2, inventory.LowStockCount);
        Assert.Equal(1, inventory.CriticalStockCount);
        Assert.Equal(2, inventory.ReservedInventoryItems);
        Assert.Equal(21, inventory.TotalQuantityAvailable);
        Assert.Equal(7, inventory.TotalQuantityReserved);
        Assert.Equal(28, inventory.TotalQuantityOnHand);
        Assert.Equal(176m, inventory.EstimatedCostValue);
        Assert.Equal(700m, inventory.EstimatedRetailValue);
        Assert.Contains(inventory.TopReservedItems, item =>
            item.ProductSku == "OPS-HEALTHY-STOCK-SKU" &&
            item.QuantityReserved == 4);
    }


    [Fact]
    public async Task Product_listing_supports_gate13_search_filtering_effective_price_and_pagination()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var now = DateTime.UtcNow;
        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var targetCategory = await TestDataSeeder.CreateCategoryAsync(dbContext, "gate13-target");
            var otherCategory = await TestDataSeeder.CreateCategoryAsync(dbContext, "gate13-other");
            var targetBrand = await TestDataSeeder.CreateBrandAsync(dbContext, "gate13-brand");
            var otherBrand = await TestDataSeeder.CreateBrandAsync(dbContext, "gate13-other-brand");

            var activeSale = await TestDataSeeder.CreateProductAsync(
                dbContext,
                targetCategory,
                "G13-ACTIVE-SALE-SKU",
                brand: targetBrand,
                salePrice: 9m,
                saleStartAtUtc: now.AddDays(-1),
                saleEndAtUtc: now.AddDays(1));
            activeSale.Name = "Gate13 Active Sale Headphones";
            activeSale.Description = "Searchable gate thirteen active sale product.";
            await TestDataSeeder.CreateProductImageAsync(dbContext, activeSale);
            await TestDataSeeder.CreateProductSpecificationAsync(dbContext, activeSale);

            var regular = await TestDataSeeder.CreateProductAsync(
                dbContext,
                targetCategory,
                "G13-REGULAR-SKU",
                brand: targetBrand);
            regular.Name = "Gate13 Regular Product";

            var expiredSale = await TestDataSeeder.CreateProductAsync(
                dbContext,
                targetCategory,
                "G13-EXPIRED-SALE-SKU",
                brand: targetBrand,
                salePrice: 6m,
                saleStartAtUtc: now.AddDays(-4),
                saleEndAtUtc: now.AddDays(-2));

            await TestDataSeeder.CreateProductAsync(
                dbContext,
                targetCategory,
                "G13-OTHER-BRAND-SKU",
                brand: otherBrand,
                salePrice: 8m,
                saleStartAtUtc: now.AddDays(-1),
                saleEndAtUtc: now.AddDays(1));

            await TestDataSeeder.CreateProductAsync(
                dbContext,
                otherCategory,
                "G13-OTHER-CATEGORY-SKU",
                brand: targetBrand,
                salePrice: 7m,
                saleStartAtUtc: now.AddDays(-1),
                saleEndAtUtc: now.AddDays(1));

            await dbContext.SaveChangesAsync();

            return new
            {
                ActiveSaleId = activeSale.Id,
                RegularId = regular.Id,
                ExpiredSaleId = expiredSale.Id
            };
        });

        var searchResult = await client.GetFromJsonAsync<PaginatedResponse<ProductResponse>>(
            "/api/products?search=G13-ACTIVE-SALE-SKU");
        var filteredResult = await client.GetFromJsonAsync<PaginatedResponse<ProductResponse>>(
            "/api/products?categorySlug=gate13-target&brandSlug=gate13-brand&activeSaleOnly=true");
        var effectivePriceResult = await client.GetFromJsonAsync<PaginatedResponse<ProductResponse>>(
            "/api/products?categorySlug=gate13-target&brandSlug=gate13-brand&minPrice=8&maxPrice=10");
        var sortedResult = await client.GetFromJsonAsync<PaginatedResponse<ProductResponse>>(
            "/api/products?categorySlug=gate13-target&brandSlug=gate13-brand&sortBy=price_asc&page=1&pageSize=10");
        var paginationResult = await client.GetFromJsonAsync<PaginatedResponse<ProductResponse>>(
            "/api/products?page=0&pageSize=500");

        Assert.NotNull(searchResult);
        Assert.Single(searchResult!.Items);
        Assert.Equal(seeded.ActiveSaleId, searchResult.Items[0].Id);
        Assert.NotEmpty(searchResult.Items[0].Images);
        Assert.NotEmpty(searchResult.Items[0].Specifications);

        Assert.NotNull(filteredResult);
        Assert.Single(filteredResult!.Items);
        Assert.Equal(seeded.ActiveSaleId, filteredResult.Items[0].Id);
        Assert.True(filteredResult.Items[0].IsSaleActive);

        Assert.NotNull(effectivePriceResult);
        Assert.Contains(effectivePriceResult!.Items, product => product.Id == seeded.ActiveSaleId);
        Assert.DoesNotContain(effectivePriceResult.Items, product => product.Id == seeded.RegularId);
        Assert.DoesNotContain(effectivePriceResult.Items, product => product.Id == seeded.ExpiredSaleId);

        Assert.NotNull(sortedResult);
        Assert.True(sortedResult!.Items.Count >= 2);
        Assert.Equal(seeded.ActiveSaleId, sortedResult.Items[0].Id);
        Assert.Equal(9m, sortedResult.Items[0].EffectivePrice);

        Assert.NotNull(paginationResult);
        Assert.Equal(1, paginationResult!.Page);
        Assert.Equal(100, paginationResult.PageSize);
    }

    [Fact]
    public async Task Admin_order_listing_supports_gate13_payment_customer_and_date_filters()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var customer = await AuthTestHelper.CreateUserAsync(
            factory,
            ApplicationRoles.Customer,
            "gate13-customer@matger.test");
        var otherCustomer = await AuthTestHelper.CreateUserAsync(
            factory,
            ApplicationRoles.Customer,
            "gate13-other@matger.test");

        var now = DateTime.UtcNow;
        var seeded = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "gate13-orders");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "G13-ORDER-FILTER-SKU");

            var matchingOrder = await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Paid);
            matchingOrder.CreatedAt = now.AddHours(-2);

            var wrongCustomerOrder = await TestDataSeeder.CreateOrderAsync(dbContext, otherCustomer.Id, product, OrderStatus.Paid);
            wrongCustomerOrder.CreatedAt = now.AddHours(-2);

            var wrongPaymentStatusOrder = await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Paid);
            wrongPaymentStatusOrder.CreatedAt = now.AddHours(-2);

            var oldOrder = await TestDataSeeder.CreateOrderAsync(dbContext, customer.Id, product, OrderStatus.Paid);
            oldOrder.CreatedAt = now.AddDays(-10);

            dbContext.Payments.AddRange(
                new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = matchingOrder.Id,
                    Order = matchingOrder,
                    Amount = matchingOrder.Total,
                    Status = PaymentStatus.Succeeded,
                    ProviderReference = "G13-MATCHING-PAYMENT",
                    CreatedAt = now.AddHours(-2),
                    ConfirmedAt = now.AddHours(-1)
                },
                new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = wrongCustomerOrder.Id,
                    Order = wrongCustomerOrder,
                    Amount = wrongCustomerOrder.Total,
                    Status = PaymentStatus.Succeeded,
                    ProviderReference = "G13-WRONG-CUSTOMER-PAYMENT",
                    CreatedAt = now.AddHours(-2),
                    ConfirmedAt = now.AddHours(-1)
                },
                new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = wrongPaymentStatusOrder.Id,
                    Order = wrongPaymentStatusOrder,
                    Amount = wrongPaymentStatusOrder.Total,
                    Status = PaymentStatus.Pending,
                    ProviderReference = "G13-PENDING-PAYMENT",
                    CreatedAt = now.AddHours(-2)
                },
                new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = oldOrder.Id,
                    Order = oldOrder,
                    Amount = oldOrder.Total,
                    Status = PaymentStatus.Succeeded,
                    ProviderReference = "G13-OLD-PAYMENT",
                    CreatedAt = now.AddDays(-10),
                    ConfirmedAt = now.AddDays(-10)
                });

            await dbContext.SaveChangesAsync();

            return new
            {
                MatchingOrderId = matchingOrder.Id
            };
        });

        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));
        var url = $"/api/orders?status=Paid&paymentStatus=Succeeded&customerEmail=gate13-customer&from={from}&to={to}&page=1&pageSize=10";

        client.UseBearerToken(admin);
        var response = await client.GetAsync(url);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<OrderResponse>>();
        var invalidPaymentStatusResponse = await client.GetAsync("/api/orders?paymentStatus=UnknownStatus");
        var invalidDateRangeResponse = await client.GetAsync($"/api/orders?from={to}&to={from}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, result!.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(seeded.MatchingOrderId, result.Items[0].Id);
        Assert.Equal("Paid", result.Items[0].Status);

        Assert.Equal(HttpStatusCode.BadRequest, invalidPaymentStatusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDateRangeResponse.StatusCode);
    }


    [Fact]
    public async Task Demo_seed_runs_idempotently_and_populates_final_commercial_dataset()
    {
        using var factory = new TestApplicationFactory();

        DemoSeedRunResult firstRun;
        DemoSeedRunResult secondRun;

        using (var scope = factory.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();

            firstRun = await seeder.SeedAsync(force: true);
            secondRun = await seeder.SeedAsync(force: true);
        }

        var counts = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            Users = await dbContext.Users.CountAsync(),
            Categories = await dbContext.Categories.CountAsync(),
            Brands = await dbContext.Brands.CountAsync(),
            Products = await dbContext.Products.CountAsync(),
            ProductImages = await dbContext.ProductImages.CountAsync(),
            ProductSpecifications = await dbContext.ProductSpecifications.CountAsync(),
            ProductPriceHistories = await dbContext.ProductPriceHistories.CountAsync(),
            ProductVariants = await dbContext.ProductVariants.CountAsync(),
            InventoryItems = await dbContext.InventoryItems.CountAsync(),
            InventoryMovements = await dbContext.InventoryMovements.CountAsync(),
            StockAdjustmentRequests = await dbContext.StockAdjustmentRequests.CountAsync(),
            Orders = await dbContext.Orders.CountAsync(),
            Payments = await dbContext.Payments.CountAsync(),
            PaymentAttempts = await dbContext.PaymentAttempts.CountAsync(),
            ReturnRequests = await dbContext.ReturnRequests.CountAsync(),
            Refunds = await dbContext.Refunds.CountAsync(),
            ProductReviews = await dbContext.ProductReviews.CountAsync(),
            CustomerInternalNotes = await dbContext.CustomerInternalNotes.CountAsync(),
            RiskSignals = await dbContext.RiskSignals.CountAsync(),
            CustomerWallets = await dbContext.CustomerWallets.CountAsync(),
            CustomerWalletTransactions = await dbContext.CustomerWalletTransactions.CountAsync(),
            LoyaltyAccounts = await dbContext.LoyaltyAccounts.CountAsync(),
            LoyaltyTransactions = await dbContext.LoyaltyTransactions.CountAsync()
        });

        Assert.True(firstRun.Enabled);
        Assert.False(firstRun.AlreadySeeded);
        Assert.True(firstRun.ProductsCreated >= 120);
        Assert.True(firstRun.OrdersCreated >= 200);
        Assert.True(firstRun.ProductImagesCreated > 0);
        Assert.True(firstRun.ProductSpecificationsCreated > 0);
        Assert.True(firstRun.ProductPriceHistoriesCreated > 0);
        Assert.True(firstRun.StockAdjustmentRequestsCreated > 0);
        Assert.True(firstRun.CustomerWalletsCreated > 0);
        Assert.True(firstRun.LoyaltyAccountsCreated > 0);
        Assert.True(firstRun.RiskSignalsCreated > 0);

        Assert.True(secondRun.AlreadySeeded);
        Assert.Equal(0, secondRun.ProductsCreated);
        Assert.Equal(0, secondRun.OrdersCreated);

        Assert.True(counts.Users >= 27);
        Assert.True(counts.Categories >= 10);
        Assert.True(counts.Brands >= 8);
        Assert.True(counts.Products >= 120);
        Assert.True(counts.ProductImages >= 240);
        Assert.True(counts.ProductSpecifications >= 300);
        Assert.True(counts.ProductPriceHistories >= 120);
        Assert.True(counts.ProductVariants > 0);
        Assert.True(counts.InventoryItems >= 120);
        Assert.True(counts.InventoryMovements > 0);
        Assert.True(counts.StockAdjustmentRequests > 0);
        Assert.True(counts.Orders >= 200);
        Assert.True(counts.Payments > 0);
        Assert.True(counts.PaymentAttempts > 0);
        Assert.True(counts.ReturnRequests > 0);
        Assert.True(counts.Refunds > 0);
        Assert.True(counts.ProductReviews > 0);
        Assert.True(counts.CustomerInternalNotes > 0);
        Assert.True(counts.RiskSignals > 0);
        Assert.True(counts.CustomerWallets > 0);
        Assert.True(counts.CustomerWalletTransactions > 0);
        Assert.True(counts.LoyaltyAccounts > 0);
        Assert.True(counts.LoyaltyTransactions > 0);
    }

    [Fact]
    public async Task Demo_summary_is_admin_only_and_exposes_final_demo_counts()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        using (var scope = factory.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
            await seeder.SeedAsync(force: true);
        }

        client.UseBearerToken(customer);
        var customerResponse = await client.GetAsync("/api/admin/demo-data/summary");

        client.UseBearerToken(admin);
        var adminResponse = await client.GetAsync("/api/admin/demo-data/summary");
        var summary = await adminResponse.Content.ReadFromJsonAsync<DemoDataSummaryResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.NotNull(summary);
        Assert.True(summary!.DemoSeedEnabled);
        Assert.Equal("admin@matger.local", summary.AdminEmail);
        Assert.Equal("customer01@matger.local", summary.FirstCustomerEmail);
        Assert.True(summary.Products >= 120);
        Assert.True(summary.ProductImages >= 240);
        Assert.True(summary.ProductSpecifications >= 300);
        Assert.True(summary.ProductPriceHistories >= 120);
        Assert.True(summary.Orders >= 200);
        Assert.True(summary.StockAdjustmentRequests > 0);
        Assert.True(summary.CustomerWallets > 0);
        Assert.True(summary.CustomerWalletTransactions > 0);
        Assert.True(summary.LoyaltyAccounts > 0);
        Assert.True(summary.LoyaltyTransactions > 0);
        Assert.True(summary.CustomerInternalNotes > 0);
        Assert.True(summary.RiskSignals > 0);
        Assert.True(summary.AuditLogs > 0);
    }

    private static async Task<HttpResponseMessage> StartCheckoutAsync(
        HttpClient client,
        Guid shippingAddressId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/start")
        {
            Content = JsonContent.Create(new CheckoutStartRequest
            {
                ShippingAddressId = shippingAddressId
            })
        };

        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Gate15_wallet_endpoints_require_customer_role()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var anonymousWalletResponse = await client.GetAsync("/api/wallet");

        client.UseBearerToken(admin);
        var adminWalletResponse = await client.GetAsync("/api/wallet");
        var adminTransactionsResponse = await client.GetAsync("/api/wallet/transactions");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousWalletResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminWalletResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminTransactionsResponse.StatusCode);
    }

    [Fact]
    public async Task Gate15_loyalty_endpoints_require_customer_role()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);

        var anonymousLoyaltyResponse = await client.GetAsync("/api/loyalty");

        client.UseBearerToken(admin);
        var adminLoyaltyResponse = await client.GetAsync("/api/loyalty");
        var adminTransactionsResponse = await client.GetAsync("/api/loyalty/transactions");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousLoyaltyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminLoyaltyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminTransactionsResponse.StatusCode);
    }

    [Fact]
    public async Task Gate15_wallet_transactions_are_scoped_to_current_customer()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var firstCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var secondCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);

        client.UseBearerToken(admin);
        var firstCreditResponse = await client.PostAsJsonAsync($"/api/admin/customers/{firstCustomer.Id}/wallet/credit", new
        {
            Amount = 15m,
            Note = "Gate15 first customer wallet credit"
        });
        var secondCreditResponse = await client.PostAsJsonAsync($"/api/admin/customers/{secondCustomer.Id}/wallet/credit", new
        {
            Amount = 40m,
            Note = "Gate15 second customer wallet credit"
        });

        client.UseBearerToken(firstCustomer);
        var firstCustomerTransactionsResponse = await client.GetAsync("/api/wallet/transactions");
        var firstCustomerTransactions = await firstCustomerTransactionsResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<CustomerWalletTransactionResponse>>();

        Assert.Equal(HttpStatusCode.OK, firstCreditResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondCreditResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstCustomerTransactionsResponse.StatusCode);
        Assert.NotNull(firstCustomerTransactions);
        Assert.Contains(firstCustomerTransactions!, transaction => transaction.Amount == 15m && transaction.Type == "Credit");
        Assert.DoesNotContain(firstCustomerTransactions, transaction => transaction.Amount == 40m);
    }

    [Fact]
    public async Task Gate15_loyalty_transactions_are_scoped_to_current_customer()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var firstCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var secondCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);

        client.UseBearerToken(admin);
        var firstAdjustResponse = await client.PostAsJsonAsync($"/api/admin/customers/{firstCustomer.Id}/loyalty/adjust", new
        {
            Points = 12,
            Note = "Gate15 first customer loyalty adjustment"
        });
        var secondAdjustResponse = await client.PostAsJsonAsync($"/api/admin/customers/{secondCustomer.Id}/loyalty/adjust", new
        {
            Points = 50,
            Note = "Gate15 second customer loyalty adjustment"
        });

        client.UseBearerToken(firstCustomer);
        var firstCustomerTransactionsResponse = await client.GetAsync("/api/loyalty/transactions");
        var firstCustomerTransactions = await firstCustomerTransactionsResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<LoyaltyTransactionResponse>>();

        Assert.Equal(HttpStatusCode.OK, firstAdjustResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondAdjustResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstCustomerTransactionsResponse.StatusCode);
        Assert.NotNull(firstCustomerTransactions);
        Assert.Contains(firstCustomerTransactions!, transaction => transaction.Points == 12 && transaction.Type == "Adjusted");
        Assert.DoesNotContain(firstCustomerTransactions, transaction => transaction.Points == 50);
    }

    [Fact]
    public async Task Gate15_admin_loyalty_summary_is_admin_only_and_counts_transactions()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);

        client.UseBearerToken(customer);
        var customerSummaryResponse = await client.GetAsync("/api/admin/loyalty/summary");

        client.UseBearerToken(admin);
        var adjustmentResponse = await client.PostAsJsonAsync($"/api/admin/customers/{customer.Id}/loyalty/adjust", new
        {
            Points = 25,
            Note = "Gate15 summary adjustment"
        });
        var adminSummaryResponse = await client.GetAsync("/api/admin/loyalty/summary");
        var summary = await adminSummaryResponse.Content.ReadFromJsonAsync<LoyaltySummaryResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, customerSummaryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adjustmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminSummaryResponse.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.Accounts);
        Assert.Equal(25, summary.PointsOutstanding);
        Assert.Equal(1, summary.Transactions);
    }

    [Fact]
    public async Task Gate15_wishlist_add_is_idempotent_and_remove_clears_item()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var productId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "gate15-wishlist");
            var product = await TestDataSeeder.CreateProductAsync(dbContext, category, "G15-WISHLIST-SKU");

            return product.Id;
        });

        client.UseBearerToken(customer);
        var firstAddResponse = await client.PostAsync($"/api/wishlist/{productId}", null);
        var secondAddResponse = await client.PostAsync($"/api/wishlist/{productId}", null);
        var listResponse = await client.GetAsync("/api/wishlist");
        var list = await listResponse.Content.ReadFromJsonAsync<PaginatedResponse<WishlistItemResponse>>();
        var removeResponse = await client.DeleteAsync($"/api/wishlist/{productId}");
        var afterRemoveResponse = await client.GetAsync("/api/wishlist");
        var afterRemove = await afterRemoveResponse.Content.ReadFromJsonAsync<PaginatedResponse<WishlistItemResponse>>();

        Assert.Equal(HttpStatusCode.Created, firstAddResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondAddResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(list);
        Assert.Equal(1, list!.TotalCount);
        Assert.Single(list.Items);
        Assert.Equal(productId, list.Items[0].ProductId);
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterRemoveResponse.StatusCode);
        Assert.NotNull(afterRemove);
        Assert.Equal(0, afterRemove!.TotalCount);
    }

    [Fact]
    public async Task Gate15_wishlist_rejects_inactive_product_and_non_customer_roles()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Admin);
        var customer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var inactiveProductId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.CreateCategoryAsync(dbContext, "gate15-inactive-wishlist");
            var inactiveProduct = await TestDataSeeder.CreateProductAsync(
                dbContext,
                category,
                "G15-INACTIVE-WISHLIST-SKU",
                isActive: false);

            return inactiveProduct.Id;
        });

        var anonymousResponse = await client.PostAsync($"/api/wishlist/{inactiveProductId}", null);

        client.UseBearerToken(admin);
        var adminResponse = await client.PostAsync($"/api/wishlist/{inactiveProductId}", null);

        client.UseBearerToken(customer);
        var customerResponse = await client.PostAsync($"/api/wishlist/{inactiveProductId}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, customerResponse.StatusCode);
    }

    [Fact]
    public async Task Gate15_addresses_are_customer_scoped_and_soft_delete_hides_deleted_address()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var firstCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var secondCustomer = await AuthTestHelper.CreateUserAsync(factory, ApplicationRoles.Customer);
        var firstCustomerAddressId = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var firstAddress = await TestDataSeeder.CreateAddressAsync(dbContext, firstCustomer.Id);
            await TestDataSeeder.CreateAddressAsync(dbContext, secondCustomer.Id);

            return firstAddress.Id;
        });

        client.UseBearerToken(secondCustomer);
        var crossCustomerResponse = await client.GetAsync($"/api/addresses/{firstCustomerAddressId}");

        client.UseBearerToken(firstCustomer);
        var deleteResponse = await client.DeleteAsync($"/api/addresses/{firstCustomerAddressId}");
        var deletedAddressResponse = await client.GetAsync($"/api/addresses/{firstCustomerAddressId}");
        var allAddressesResponse = await client.GetAsync("/api/addresses");
        var allAddresses = await allAddressesResponse.Content.ReadFromJsonAsync<IReadOnlyList<AddressResponse>>();

        Assert.Equal(HttpStatusCode.NotFound, crossCustomerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedAddressResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allAddressesResponse.StatusCode);
        Assert.NotNull(allAddresses);
        Assert.DoesNotContain(allAddresses!, address => address.Id == firstCustomerAddressId);
    }

}
