using MATGER.Api.Data;
using MATGER.Api.DTOs.Demo;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MATGER.Api.Identity;

public sealed class DemoDataSeeder(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IOptions<DemoSeedOptions> demoSeedOptions,
    ILogger<DemoDataSeeder> logger)
{
    private const string AdminEmail = "admin@matger.local";
    private const string OrderManagerEmail = "order.manager@matger.local";
    private const string InventoryManagerEmail = "inventory.manager@matger.local";
    private const string FirstCustomerEmail = "customer01@matger.local";
    private const string DemoSkuPrefix = "DEMO-";

    public async Task<DemoSeedRunResult> SeedAsync(
        CancellationToken cancellationToken = default,
        bool force = false)
    {
        var options = NormalizeOptions(demoSeedOptions.Value);

        var result = new DemoSeedRunResult
        {
            Enabled = options.Enabled,
            DemoPassword = options.DemoPassword,
            AdminEmail = AdminEmail,
            OrderManagerEmail = OrderManagerEmail,
            InventoryManagerEmail = InventoryManagerEmail,
            FirstCustomerEmail = FirstCustomerEmail
        };

        if (!options.Enabled && !force)
        {
            result.Message = "Demo seed is disabled. Set DemoSeed:Enabled=true or call the manual seed endpoint.";
            return result;
        }

        var random = new Random(options.RandomSeed);
        var now = DateTime.UtcNow;

        var alreadySeeded = await dbContext.Products
            .AnyAsync(product => product.SKU.StartsWith(DemoSkuPrefix), cancellationToken);

        if (alreadySeeded)
        {
            await EnsureCommercialPresentationAsync(result, now, cancellationToken);

            result.AlreadySeeded = true;
            result.Message = result.BrandsCreated + result.ProductImagesCreated + result.ProductSpecificationsCreated > 0
                ? "Demo data already existed. Commercial presentation data was topped up safely."
                : "Demo data already exists. No rows were added.";
            return result;
        }

        var adminUser = await EnsureUserAsync(
            AdminEmail,
            "MATGER Admin",
            "07710000000",
            ApplicationRoles.Admin,
            "Admin12345",
            result,
            cancellationToken);

        var orderManager = await EnsureUserAsync(
            OrderManagerEmail,
            "Demo Order Manager",
            "07710000001",
            ApplicationRoles.OrderManager,
            options.DemoPassword,
            result,
            cancellationToken);

        var inventoryManager = await EnsureUserAsync(
            InventoryManagerEmail,
            "Demo Inventory Manager",
            "07710000002",
            ApplicationRoles.InventoryManager,
            options.DemoPassword,
            result,
            cancellationToken);

        var customers = new List<ApplicationUser>();

        for (var index = 1; index <= options.CustomerCount; index++)
        {
            var customer = await EnsureUserAsync(
                $"customer{index:00}@matger.local",
                DemoCustomerNames[(index - 1) % DemoCustomerNames.Length],
                $"0772{index:0000000}",
                ApplicationRoles.Customer,
                options.DemoPassword,
                result,
                cancellationToken);

            customers.Add(customer);
        }

        var categories = CreateCategories();
        dbContext.Categories.AddRange(categories);
        result.CategoriesCreated = categories.Count;

        var brands = CreateBrands(now);
        dbContext.Brands.AddRange(brands);
        result.BrandsCreated = brands.Count;

        var shippingMethods = CreateShippingMethods(now);
        dbContext.ShippingMethods.AddRange(shippingMethods);
        result.ShippingMethodsCreated = shippingMethods.Count;

        var coupons = CreateCoupons(now);
        dbContext.Coupons.AddRange(coupons);
        result.CouponsCreated = coupons.Count;

        var catalog = CreateCatalog(
            categories,
            brands,
            options.ProductsPerCategory,
            random,
            now,
            result);

        dbContext.Products.AddRange(catalog.Products);
        dbContext.InventoryItems.AddRange(catalog.InventoryItems);
        dbContext.InventoryMovements.AddRange(catalog.InitialInventoryMovements);

        var addresses = CreateCustomerAddresses(customers, now, result);
        dbContext.CustomerAddresses.AddRange(addresses);

        var wishlists = CreateWishlistItems(customers, catalog.Products, random, now, result);
        dbContext.WishlistItems.AddRange(wishlists);

        var activeCarts = CreateActiveCarts(customers, catalog.Products, random, now, result);
        dbContext.Carts.AddRange(activeCarts);

        var customerWallets = CreateCustomerWallets(adminUser, customers, now, result);
        dbContext.CustomerWallets.AddRange(customerWallets.Wallets);

        var loyaltyAccounts = CreateLoyaltyAccounts(customers, now, result);
        dbContext.LoyaltyAccounts.AddRange(loyaltyAccounts.Accounts);

        var orders = CreateOrders(
            customers,
            adminUser,
            orderManager,
            inventoryManager,
            addresses,
            catalog.Products,
            shippingMethods,
            coupons,
            random,
            now,
            options.OrderCount,
            result);

        dbContext.Orders.AddRange(orders.Orders);
        dbContext.InventoryReservations.AddRange(orders.InventoryReservations);
        dbContext.Payments.AddRange(orders.Payments);
        dbContext.PaymentAttempts.AddRange(orders.PaymentAttempts);
        dbContext.CouponRedemptions.AddRange(orders.CouponRedemptions);
        dbContext.ReturnRequests.AddRange(orders.ReturnRequests);
        dbContext.Refunds.AddRange(orders.Refunds);
        dbContext.ProductReviews.AddRange(orders.ProductReviews);
        dbContext.OrderStatusHistories.AddRange(orders.StatusHistories);
        dbContext.OrderInternalNotes.AddRange(orders.InternalNotes);
        dbContext.InventoryMovements.AddRange(orders.InventoryMovements);

        var stockAdjustments = CreateStockAdjustmentRequests(
            adminUser,
            inventoryManager,
            catalog.Products,
            now,
            result);
        dbContext.StockAdjustmentRequests.AddRange(stockAdjustments.Requests);
        dbContext.InventoryMovements.AddRange(stockAdjustments.InventoryMovements);

        var customerInternalNotes = CreateCustomerInternalNotes(
            adminUser,
            orderManager,
            customers,
            now,
            result);
        dbContext.CustomerInternalNotes.AddRange(customerInternalNotes);

        var riskSignals = CreateRiskSignals(
            adminUser,
            orders.Orders,
            now,
            result);
        dbContext.RiskSignals.AddRange(riskSignals);

        var auditLogs = CreateAuditLogs(adminUser, orderManager, inventoryManager, catalog.Products, orders.Orders, now, result);
        dbContext.AuditLogs.AddRange(auditLogs);

        await dbContext.SaveChangesAsync(cancellationToken);

        result.Message = "Large MATGER demo data set seeded successfully.";

        logger.LogInformation(
            "MATGER demo data seeded. Products={Products}, Orders={Orders}, Customers={Customers}",
            result.ProductsCreated,
            result.OrdersCreated,
            result.CustomersCreated);

        return result;
    }

    private async Task<ApplicationUser> EnsureUserAsync(
        string email,
        string fullName,
        string phoneNumber,
        string role,
        string password,
        DemoSeedRunResult result,
        CancellationToken cancellationToken)
    {
        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            if (!await userManager.IsInRoleAsync(existingUser, role))
            {
                var roleResult = await userManager.AddToRoleAsync(existingUser, role);

                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to assign role '{role}' to '{email}': {FormatIdentityErrors(roleResult)}");
                }
            }

            if (!existingUser.IsActive)
            {
                existingUser.IsActive = true;
                existingUser.UpdatedAt = DateTime.UtcNow;

                var updateResult = await userManager.UpdateAsync(existingUser);

                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to reactivate demo user '{email}': {FormatIdentityErrors(updateResult)}");
                }
            }

            return existingUser;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            PhoneNumber = phoneNumber,
            PhoneNumberConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-120)
        };

        var createResult = await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create demo user '{email}': {FormatIdentityErrors(createResult)}");
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, role);

        if (!addRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign role '{role}' to demo user '{email}': {FormatIdentityErrors(addRoleResult)}");
        }

        result.CustomersCreated += role == ApplicationRoles.Customer ? 1 : 0;

        return user;
    }

    private async Task EnsureCommercialPresentationAsync(
        DemoSeedRunResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var demoBrandSlugs = DemoBrands
            .Select(brand => brand.Slug)
            .ToArray();

        var existingBrands = await dbContext.Brands
            .Where(brand => demoBrandSlugs.Contains(brand.Slug))
            .ToListAsync(cancellationToken);

        var brandsBySlug = existingBrands.ToDictionary(
            brand => brand.Slug,
            StringComparer.OrdinalIgnoreCase);

        foreach (var brandSpec in DemoBrands)
        {
            if (brandsBySlug.ContainsKey(brandSpec.Slug))
            {
                continue;
            }

            var brand = new Brand
            {
                Id = Guid.NewGuid(),
                Name = brandSpec.Name,
                Slug = brandSpec.Slug,
                IsActive = true,
                CreatedAt = now.AddDays(-60 + brandsBySlug.Count)
            };

            dbContext.Brands.Add(brand);
            brandsBySlug.Add(brand.Slug, brand);
            result.BrandsCreated++;
        }

        var brands = DemoBrands
            .Select(brand => brandsBySlug[brand.Slug])
            .ToList();

        var demoProducts = await dbContext.Products
            .Include(product => product.Category)
            .Include(product => product.InventoryItem)
            .Include(product => product.Images)
            .Include(product => product.Specifications)
            .Include(product => product.PriceHistories)
            .AsSplitQuery()
            .Where(product => product.SKU.StartsWith(DemoSkuPrefix))
            .OrderBy(product => product.SKU)
            .ToListAsync(cancellationToken);

        for (var index = 0; index < demoProducts.Count; index++)
        {
            var product = demoProducts[index];
            var categorySpec = DemoCategories.FirstOrDefault(spec => spec.Slug == product.Category.Slug)
                ?? DemoCategories[0];

            if (product.BrandId is null)
            {
                var brand = brands[index % brands.Count];
                product.BrandId = brand.Id;
                product.Brand = brand;
            }

            if (product.CostPrice is null)
            {
                product.CostPrice = Money(product.Price * 0.62m);
            }

            if (!product.SalePrice.HasValue)
            {
                ApplyDemoSale(product, index, now);
            }

            if (product.PriceHistories.Count == 0)
            {
                var history = CreateDemoPriceHistory(product, now.AddDays(-20).AddMinutes(index));
                dbContext.ProductPriceHistories.Add(history);
                result.ProductPriceHistoriesCreated++;
            }

            if (product.Images.Count == 0)
            {
                var images = CreateProductImages(product, categorySpec, index, now);
                dbContext.ProductImages.AddRange(images);
                result.ProductImagesCreated += images.Count;
            }

            if (product.Specifications.Count == 0)
            {
                var specifications = CreateProductSpecifications(product, categorySpec, index);
                dbContext.ProductSpecifications.AddRange(specifications);
                result.ProductSpecificationsCreated += specifications.Count;
            }

            if (product.InventoryItem is not null &&
                (product.InventoryItem.ReorderPoint is null ||
                 string.IsNullOrWhiteSpace(product.InventoryItem.SupplierName)))
            {
                product.InventoryItem.SupplierName = DemoSuppliers[index % DemoSuppliers.Length];
                product.InventoryItem.SupplierSku = $"SUP-{product.SKU.Replace("DEMO-", string.Empty, StringComparison.OrdinalIgnoreCase)}";
                product.InventoryItem.ReorderPoint = index % 12 == 0
                    ? 20
                    : Math.Max(product.InventoryItem.LowStockThreshold + 5, 15);
                product.InventoryItem.ReorderQuantity = 60 + index % 6 * 20;
                product.InventoryItem.LeadTimeDays = 4 + index % 10;
                product.InventoryItem.BinLocation = $"B{index % 12 + 1:00}-{index % 9 + 1:00}";

                if (index % 12 == 0)
                {
                    product.InventoryItem.QuantityAvailable = 0;
                }
                else if (index % 8 == 0 &&
                         product.InventoryItem.QuantityAvailable > product.InventoryItem.ReorderPoint.Value)
                {
                    product.InventoryItem.QuantityAvailable = Math.Max(1, product.InventoryItem.ReorderPoint.Value - 2);
                }
            }
        }

        var orderItemsMissingCostSnapshot = await dbContext.OrderItems
            .Include(item => item.Product)
            .Where(item =>
                item.CostPriceSnapshot == null &&
                item.Product.SKU.StartsWith(DemoSkuPrefix))
            .ToListAsync(cancellationToken);

        foreach (var item in orderItemsMissingCostSnapshot)
        {
            item.CostPriceSnapshot = item.Product.CostPrice ?? Money(item.UnitPrice * 0.62m);
        }

        await EnsureStockAdjustmentRequestsAsync(result, now, cancellationToken);
        await EnsureCustomerInternalNotesAsync(result, now, cancellationToken);
        await EnsureRiskSignalsAsync(result, now, cancellationToken);
        await EnsureCustomerWalletsAsync(result, now, cancellationToken);
        await EnsureLoyaltyAccountsAsync(result, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureStockAdjustmentRequestsAsync(
        DemoSeedRunResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var alreadyCreated = await dbContext.StockAdjustmentRequests
            .AnyAsync(request => request.Product.SKU.StartsWith(DemoSkuPrefix), cancellationToken);

        if (alreadyCreated)
        {
            return;
        }

        var adminUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == AdminEmail, cancellationToken);
        var inventoryManager = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == InventoryManagerEmail, cancellationToken);

        if (adminUser is null || inventoryManager is null)
        {
            return;
        }

        var products = await dbContext.Products
            .Include(product => product.InventoryItem)
            .Where(product => product.SKU.StartsWith(DemoSkuPrefix))
            .OrderBy(product => product.SKU)
            .Take(6)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return;
        }

        var seed = CreateStockAdjustmentRequests(
            adminUser,
            inventoryManager,
            products,
            now,
            result);

        dbContext.StockAdjustmentRequests.AddRange(seed.Requests);
        dbContext.InventoryMovements.AddRange(seed.InventoryMovements);
    }

    private async Task EnsureCustomerInternalNotesAsync(
        DemoSeedRunResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var alreadyCreated = await dbContext.CustomerInternalNotes
            .AnyAsync(note => note.Customer.Email != null &&
                note.Customer.Email.EndsWith("@matger.local"), cancellationToken);

        if (alreadyCreated)
        {
            return;
        }

        var adminUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == AdminEmail, cancellationToken);
        var orderManager = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == OrderManagerEmail, cancellationToken);

        if (adminUser is null || orderManager is null)
        {
            return;
        }

        var customers = await dbContext.Users
            .Where(user => user.Email != null &&
                user.Email.StartsWith("customer") &&
                user.Email.EndsWith("@matger.local"))
            .OrderBy(user => user.Email)
            .Take(12)
            .ToListAsync(cancellationToken);

        if (customers.Count == 0)
        {
            return;
        }

        var notes = CreateCustomerInternalNotes(
            adminUser,
            orderManager,
            customers,
            now,
            result);

        dbContext.CustomerInternalNotes.AddRange(notes);
    }

    private async Task EnsureRiskSignalsAsync(
        DemoSeedRunResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var alreadyCreated = await dbContext.RiskSignals
            .AnyAsync(signal => signal.Order != null &&
                signal.Order.OrderNumber.StartsWith("DEMO-ORD-"), cancellationToken);

        if (alreadyCreated)
        {
            return;
        }

        var adminUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == AdminEmail, cancellationToken);

        if (adminUser is null)
        {
            return;
        }

        var orders = await dbContext.Orders
            .Include(order => order.User)
            .Where(order => order.OrderNumber.StartsWith("DEMO-ORD-"))
            .OrderBy(order => order.OrderNumber)
            .Take(14)
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return;
        }

        var signals = CreateRiskSignals(
            adminUser,
            orders,
            now,
            result);

        dbContext.RiskSignals.AddRange(signals);
    }

    private async Task EnsureCustomerWalletsAsync(
        DemoSeedRunResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var alreadyCreated = await dbContext.CustomerWallets
            .AnyAsync(wallet => wallet.User.Email != null &&
                wallet.User.Email.EndsWith("@matger.local"), cancellationToken);

        if (alreadyCreated)
        {
            return;
        }

        var adminUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == AdminEmail, cancellationToken);

        if (adminUser is null)
        {
            return;
        }

        var customers = await dbContext.Users
            .Where(user => user.Email != null &&
                user.Email.StartsWith("customer") &&
                user.Email.EndsWith("@matger.local"))
            .OrderBy(user => user.Email)
            .Take(18)
            .ToListAsync(cancellationToken);

        if (customers.Count == 0)
        {
            return;
        }

        var wallets = CreateCustomerWallets(adminUser, customers, now, result);
        dbContext.CustomerWallets.AddRange(wallets.Wallets);
    }

    private async Task EnsureLoyaltyAccountsAsync(
        DemoSeedRunResult result,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var alreadyCreated = await dbContext.LoyaltyAccounts
            .AnyAsync(account => account.User.Email != null &&
                account.User.Email.EndsWith("@matger.local"), cancellationToken);

        if (alreadyCreated)
        {
            return;
        }

        var customers = await dbContext.Users
            .Where(user => user.Email != null &&
                user.Email.StartsWith("customer") &&
                user.Email.EndsWith("@matger.local"))
            .OrderBy(user => user.Email)
            .Take(18)
            .ToListAsync(cancellationToken);

        if (customers.Count == 0)
        {
            return;
        }

        var accounts = CreateLoyaltyAccounts(customers, now, result);
        dbContext.LoyaltyAccounts.AddRange(accounts.Accounts);
    }

    private static List<Category> CreateCategories()
    {
        return DemoCategories
            .Select(category => new Category
            {
                Id = Guid.NewGuid(),
                Name = category.Name,
                Slug = category.Slug,
                IsActive = true
            })
            .ToList();
    }

    private static List<Brand> CreateBrands(DateTime now)
    {
        return DemoBrands
            .Select((brand, index) => new Brand
            {
                Id = Guid.NewGuid(),
                Name = brand.Name,
                Slug = brand.Slug,
                IsActive = true,
                CreatedAt = now.AddDays(-120 + index)
            })
            .ToList();
    }

    private static List<ShippingMethod> CreateShippingMethods(DateTime now)
    {
        return
        [
            new ShippingMethod
            {
                Id = Guid.NewGuid(),
                Name = "Baghdad Same Day",
                Code = "DEMO-BGD-SAME-DAY",
                BaseCost = 5000m,
                EstimatedDeliveryDays = 1,
                IsActive = true,
                CreatedAt = now.AddDays(-90)
            },
            new ShippingMethod
            {
                Id = Guid.NewGuid(),
                Name = "Iraq Standard Delivery",
                Code = "DEMO-IRQ-STANDARD",
                BaseCost = 7000m,
                EstimatedDeliveryDays = 3,
                IsActive = true,
                CreatedAt = now.AddDays(-90)
            },
            new ShippingMethod
            {
                Id = Guid.NewGuid(),
                Name = "Express Courier",
                Code = "DEMO-EXPRESS",
                BaseCost = 12000m,
                EstimatedDeliveryDays = 2,
                IsActive = true,
                CreatedAt = now.AddDays(-90)
            },
            new ShippingMethod
            {
                Id = Guid.NewGuid(),
                Name = "Pickup From Warehouse",
                Code = "DEMO-PICKUP",
                BaseCost = 0m,
                EstimatedDeliveryDays = 1,
                IsActive = true,
                CreatedAt = now.AddDays(-90)
            }
        ];
    }

    private static List<Coupon> CreateCoupons(DateTime now)
    {
        return
        [
            new Coupon
            {
                Id = Guid.NewGuid(),
                Code = "DEMO10",
                Name = "Demo 10% Off",
                Description = "Demo percentage coupon for public store campaigns.",
                DiscountType = CouponDiscountType.Percentage,
                DiscountValue = 10m,
                MaxDiscountAmount = 25000m,
                MinimumOrderSubtotal = 50000m,
                StartsAt = now.AddDays(-45),
                ExpiresAt = now.AddDays(45),
                IsActive = true,
                UsageLimit = 500,
                PerCustomerUsageLimit = 3,
                CreatedAt = now.AddDays(-45)
            },
            new Coupon
            {
                Id = Guid.NewGuid(),
                Code = "WELCOME25",
                Name = "Welcome Demo Discount",
                Description = "Fixed amount coupon used by new demo customers.",
                DiscountType = CouponDiscountType.FixedAmount,
                DiscountValue = 25000m,
                MaxDiscountAmount = null,
                MinimumOrderSubtotal = 100000m,
                StartsAt = now.AddDays(-60),
                ExpiresAt = now.AddDays(60),
                IsActive = true,
                UsageLimit = 300,
                PerCustomerUsageLimit = 1,
                CreatedAt = now.AddDays(-60)
            },
            new Coupon
            {
                Id = Guid.NewGuid(),
                Code = "BAGHDAD15",
                Name = "Baghdad Campaign 15%",
                Description = "Regional demo promotion for Baghdad orders.",
                DiscountType = CouponDiscountType.Percentage,
                DiscountValue = 15m,
                MaxDiscountAmount = 40000m,
                MinimumOrderSubtotal = 150000m,
                StartsAt = now.AddDays(-20),
                ExpiresAt = now.AddDays(20),
                IsActive = true,
                UsageLimit = 250,
                PerCustomerUsageLimit = 2,
                CreatedAt = now.AddDays(-20)
            },
            new Coupon
            {
                Id = Guid.NewGuid(),
                Code = "EXPIRED-DEMO",
                Name = "Expired Demo Campaign",
                Description = "Expired coupon used to make admin coupon reports realistic.",
                DiscountType = CouponDiscountType.FixedAmount,
                DiscountValue = 15000m,
                MaxDiscountAmount = null,
                MinimumOrderSubtotal = 75000m,
                StartsAt = now.AddDays(-120),
                ExpiresAt = now.AddDays(-30),
                IsActive = false,
                UsageLimit = 100,
                PerCustomerUsageLimit = 1,
                CreatedAt = now.AddDays(-120)
            }
        ];
    }

    private static CatalogSeed CreateCatalog(
        IReadOnlyList<Category> categories,
        IReadOnlyList<Brand> brands,
        int productsPerCategory,
        Random random,
        DateTime now,
        DemoSeedRunResult result)
    {
        var products = new List<Product>();
        var inventoryItems = new List<InventoryItem>();
        var initialInventoryMovements = new List<InventoryMovement>();

        for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            var category = categories[categoryIndex];
            var categorySpec = DemoCategories.Single(spec => spec.Slug == category.Slug);
            var productNames = categorySpec.ProductNames.Take(productsPerCategory).ToArray();

            for (var index = 0; index < productNames.Length; index++)
            {
                var price = Money(RandomDecimal(random, categorySpec.MinPrice, categorySpec.MaxPrice));
                var brand = brands[(categoryIndex + index) % brands.Count];
                var costPrice = Money(price * RandomDecimal(random, 0.42m, 0.72m));
                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    CategoryId = category.Id,
                    Category = category,
                    BrandId = brand.Id,
                    Brand = brand,
                    Name = productNames[index],
                    Description = $"Demo catalog item for {category.Name}. Includes realistic pricing, stock, dimensions and order history.",
                    SKU = $"{DemoSkuPrefix}{categorySpec.Code}-{index + 1:000}",
                    Price = price,
                    CostPrice = costPrice,
                    IsActive = index % 11 != 10,
                    IsFeatured = index % 4 == 0,
                    WeightKg = Money(RandomDecimal(random, 0.15m, 4.5m), 3),
                    LengthCm = Money(RandomDecimal(random, 8m, 60m)),
                    WidthCm = Money(RandomDecimal(random, 6m, 45m)),
                    HeightCm = Money(RandomDecimal(random, 2m, 35m)),
                    IsReturnable = index % 9 != 8,
                    ReturnWindowDays = index % 5 == 0 ? 7 : 14,
                    CreatedAt = now.AddDays(-random.Next(10, 160))
                };

                ApplyDemoSale(product, index, now);
                product.PriceHistories.Add(CreateDemoPriceHistory(product, product.CreatedAt.AddHours(3)));

                var inventoryItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Product = product,
                    QuantityAvailable = random.Next(70, 360),
                    QuantityReserved = random.Next(0, 10),
                    LowStockThreshold = random.Next(8, 35),
                    SupplierName = DemoSuppliers[(categoryIndex + index) % DemoSuppliers.Length],
                    SupplierSku = $"SUP-{categorySpec.Code}-{index + 1:000}",
                    ReorderPoint = index % 12 == 0 ? 20 : random.Next(12, 45),
                    ReorderQuantity = random.Next(40, 160),
                    LeadTimeDays = random.Next(3, 18),
                    BinLocation = $"A{categoryIndex + 1}-{index % 8 + 1:00}",
                    CreatedAt = product.CreatedAt.AddHours(1)
                };

                if (index % 12 == 0)
                {
                    inventoryItem.QuantityAvailable = 0;
                }
                else if (index % 8 == 0)
                {
                    inventoryItem.QuantityAvailable = Math.Max(1, inventoryItem.ReorderPoint.Value - random.Next(1, 8));
                }

                product.InventoryItem = inventoryItem;
                product.Images.AddRange(CreateProductImages(product, categorySpec, index, now));
                product.Specifications.AddRange(CreateProductSpecifications(product, categorySpec, index));

                inventoryItems.Add(inventoryItem);
                products.Add(product);

                initialInventoryMovements.Add(new InventoryMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    InventoryItemId = inventoryItem.Id,
                    Type = InventoryMovementType.ManualAdjustment,
                    QuantityChange = inventoryItem.QuantityAvailable + inventoryItem.QuantityReserved,
                    QuantityAvailableBefore = 0,
                    QuantityAvailableAfter = inventoryItem.QuantityAvailable,
                    QuantityReservedBefore = 0,
                    QuantityReservedAfter = inventoryItem.QuantityReserved,
                    Reason = "Demo seed initial stock balance.",
                    ReferenceType = "DemoSeed",
                    ReferenceId = product.SKU,
                    CreatedAt = product.CreatedAt.AddHours(2)
                });

                if (ShouldCreateVariants(categorySpec, index))
                {
                    var variants = CreateVariants(product, categorySpec, index, price, random, product.CreatedAt);

                    foreach (var variant in variants)
                    {
                        product.Variants.Add(variant);

                        initialInventoryMovements.Add(new InventoryMovement
                        {
                            Id = Guid.NewGuid(),
                            ProductId = product.Id,
                            ProductVariantId = variant.Id,
                            Type = InventoryMovementType.ManualAdjustment,
                            QuantityChange = variant.QuantityAvailable + variant.QuantityReserved,
                            QuantityAvailableBefore = 0,
                            QuantityAvailableAfter = variant.QuantityAvailable,
                            QuantityReservedBefore = 0,
                            QuantityReservedAfter = variant.QuantityReserved,
                            Reason = "Demo seed variant stock balance.",
                            ReferenceType = "DemoSeed",
                            ReferenceId = variant.SKU,
                            CreatedAt = variant.CreatedAt.AddHours(1)
                        });
                    }
                }
            }
        }

        result.ProductsCreated = products.Count;
        result.ProductImagesCreated = products.Sum(product => product.Images.Count);
        result.ProductSpecificationsCreated = products.Sum(product => product.Specifications.Count);
        result.ProductPriceHistoriesCreated = products.Sum(product => product.PriceHistories.Count);
        result.ProductVariantsCreated = products.Sum(product => product.Variants.Count);
        result.InventoryItemsCreated = inventoryItems.Count;
        result.InventoryMovementsCreated += initialInventoryMovements.Count;

        return new CatalogSeed(products, inventoryItems, initialInventoryMovements);
    }

    private static List<ProductImage> CreateProductImages(
        Product product,
        CategorySpec categorySpec,
        int productIndex,
        DateTime now)
    {
        var imageCount = 2 + productIndex % 3;
        var images = new List<ProductImage>(imageCount);
        var slug = product.SKU.ToLowerInvariant();

        for (var index = 0; index < imageCount; index++)
        {
            images.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Product = product,
                ImageUrl = $"https://images.matger.local/demo/{categorySpec.Slug}/{slug}-{index + 1}.jpg",
                AltText = $"{product.Name} demo image {index + 1}",
                IsPrimary = index == 0,
                SortOrder = index,
                CreatedAtUtc = now.AddDays(-90).AddMinutes(productIndex + index)
            });
        }

        return images;
    }

    private static List<ProductSpecification> CreateProductSpecifications(
        Product product,
        CategorySpec categorySpec,
        int productIndex)
    {
        List<ProductSpecification> specs = categorySpec.Slug switch
        {
            "demo-electronics" or "demo-mobile-accessories" or "demo-computers" or "demo-gaming" =>
            [
                CreateSpecification(product, "Connectivity", productIndex % 2 == 0 ? "USB-C / Wireless" : "Bluetooth 5.3", "Technical", 0),
                CreateSpecification(product, "Warranty", "12 months", "Commercial", 1),
                CreateSpecification(product, "Package", "Retail box with accessories", "General", 2)
            ],

            "demo-fashion" =>
            [
                CreateSpecification(product, "Material", productIndex % 2 == 0 ? "Cotton blend" : "Synthetic performance fabric", "General", 0),
                CreateSpecification(product, "Care", "Machine wash cold", "General", 1),
                CreateSpecification(product, "Fit", productIndex % 2 == 0 ? "Regular" : "Slim", "Sizing", 2)
            ],

            "demo-home-appliances" =>
            [
                CreateSpecification(product, "Power", $"{700 + productIndex * 25}W", "Technical", 0),
                CreateSpecification(product, "Warranty", "18 months", "Commercial", 1),
                CreateSpecification(product, "Safety", "Overheat protection", "General", 2)
            ],

            "demo-beauty" =>
            [
                CreateSpecification(product, "Size", $"{50 + productIndex * 5} ml", "General", 0),
                CreateSpecification(product, "Skin Type", productIndex % 2 == 0 ? "All skin types" : "Dry to normal", "Usage", 1),
                CreateSpecification(product, "Expiry", "24 months from production", "Commercial", 2)
            ],

            "demo-books-stationery" =>
            [
                CreateSpecification(product, "Format", "Retail packed", "General", 0),
                CreateSpecification(product, "Use Case", "Office and study", "General", 1),
                CreateSpecification(product, "Warranty", "7 days replacement", "Commercial", 2)
            ],

            "demo-sports" =>
            [
                CreateSpecification(product, "Use", productIndex % 2 == 0 ? "Indoor training" : "Outdoor training", "General", 0),
                CreateSpecification(product, "Material", "Durable commercial grade", "General", 1),
                CreateSpecification(product, "Warranty", "6 months", "Commercial", 2)
            ],

            "demo-automotive" =>
            [
                CreateSpecification(product, "Compatibility", "Universal fit", "Technical", 0),
                CreateSpecification(product, "Material", "Automotive grade", "General", 1),
                CreateSpecification(product, "Warranty", "12 months", "Commercial", 2)
            ],

            _ =>
            [
                CreateSpecification(product, "Warranty", "12 months", "Commercial", 0),
                CreateSpecification(product, "Package", "Retail box", "General", 1),
                CreateSpecification(product, "Origin", "Demo supplier", "General", 2)
            ]
        };

        return specs;
    }

    private static ProductSpecification CreateSpecification(
        Product product,
        string name,
        string value,
        string groupName,
        int sortOrder)
    {
        return new ProductSpecification
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            Name = name,
            Value = value,
            GroupName = groupName,
            SortOrder = sortOrder
        };
    }

    private static void ApplyDemoSale(
        Product product,
        int productIndex,
        DateTime now)
    {
        var saleBucket = productIndex % 10;

        if (saleBucket > 2)
        {
            return;
        }

        product.SalePrice = Money(product.Price * (saleBucket == 0 ? 0.82m : 0.88m));

        if (saleBucket == 0)
        {
            product.SaleStartAtUtc = now.AddDays(-5);
            product.SaleEndAtUtc = now.AddDays(15);
            return;
        }

        if (saleBucket == 1)
        {
            product.SaleStartAtUtc = now.AddDays(-30);
            product.SaleEndAtUtc = now.AddDays(-5);
            return;
        }

        product.SaleStartAtUtc = now.AddDays(3);
        product.SaleEndAtUtc = now.AddDays(20);
    }

    private static decimal GetDemoEffectivePrice(Product product, DateTime atUtc)
    {
        return product.SalePrice.HasValue &&
               product.SaleStartAtUtc.HasValue &&
               product.SaleEndAtUtc.HasValue &&
               product.SaleStartAtUtc.Value <= atUtc &&
               product.SaleEndAtUtc.Value > atUtc
            ? product.SalePrice.Value
            : product.Price;
    }

    private static ProductPriceHistory CreateDemoPriceHistory(
        Product product,
        DateTime changedAtUtc)
    {
        return new ProductPriceHistory
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            OldPrice = Money(product.Price * 1.05m),
            NewPrice = product.Price,
            OldSalePrice = null,
            NewSalePrice = product.SalePrice,
            ChangedByUserId = null,
            ChangedAtUtc = changedAtUtc,
            Reason = product.SalePrice.HasValue
                ? "Demo pricing campaign configured."
                : "Demo base price normalized.",
            ChangeType = product.SalePrice.HasValue
                ? "DemoSaleConfigured"
                : "DemoBasePriceInitialized"
        };
    }

    private static List<ProductVariant> CreateVariants(
        Product product,
        CategorySpec categorySpec,
        int productIndex,
        decimal basePrice,
        Random random,
        DateTime createdAt)
    {
        var names = GetVariantNames(categorySpec.Slug, productIndex);
        var variants = new List<ProductVariant>();

        for (var index = 0; index < names.Length; index++)
        {
            var priceChange = Money(RandomDecimal(random, -8000m, 18000m));
            var variantPrice = Math.Max(1000m, basePrice + priceChange);

            variants.Add(new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Product = product,
                Name = names[index],
                SKU = $"{product.SKU}-V{index + 1}",
                PriceOverride = index == 0 ? null : variantPrice,
                IsActive = true,
                QuantityAvailable = random.Next(30, 180),
                QuantityReserved = random.Next(0, 8),
                LowStockThreshold = random.Next(5, 20),
                CreatedAt = createdAt.AddHours(index + 1)
            });
        }

        return variants;
    }

    private static List<CustomerAddress> CreateCustomerAddresses(
        IReadOnlyList<ApplicationUser> customers,
        DateTime now,
        DemoSeedRunResult result)
    {
        var addresses = new List<CustomerAddress>();
        var areas = new[] { "Mansour", "Karrada", "Yarmouk", "Zayouna", "Jadriya", "Adhamiya", "Kadhimiya", "Dora" };

        for (var index = 0; index < customers.Count; index++)
        {
            var customer = customers[index];

            addresses.Add(new CustomerAddress
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                User = customer,
                Label = "Home",
                FullName = customer.FullName,
                PhoneNumber = customer.PhoneNumber ?? $"0772{index + 1:0000000}",
                Country = "Iraq",
                City = "Baghdad",
                Area = areas[index % areas.Length],
                Street = $"Street {10 + index}",
                Building = $"Building {1 + index % 12}",
                Floor = $"{1 + index % 5}",
                Apartment = $"{100 + index}",
                PostalCode = null,
                Notes = "Demo address used for shipping snapshots and reports.",
                IsDefault = true,
                IsDeleted = false,
                CreatedAt = now.AddDays(-100 + index)
            });

            if (index % 3 == 0)
            {
                addresses.Add(new CustomerAddress
                {
                    Id = Guid.NewGuid(),
                    UserId = customer.Id,
                    User = customer,
                    Label = "Work",
                    FullName = customer.FullName,
                    PhoneNumber = customer.PhoneNumber ?? $"0772{index + 1:0000000}",
                    Country = "Iraq",
                    City = "Baghdad",
                    Area = areas[(index + 3) % areas.Length],
                    Street = $"Commercial Street {20 + index}",
                    Building = $"Office Tower {1 + index % 8}",
                    Floor = $"{2 + index % 10}",
                    Apartment = null,
                    PostalCode = null,
                    Notes = "Secondary demo shipping address.",
                    IsDefault = false,
                    IsDeleted = false,
                    CreatedAt = now.AddDays(-80 + index)
                });
            }
        }

        result.CustomerAddressesCreated = addresses.Count;
        return addresses;
    }

    private static List<WishlistItem> CreateWishlistItems(
        IReadOnlyList<ApplicationUser> customers,
        IReadOnlyList<Product> products,
        Random random,
        DateTime now,
        DemoSeedRunResult result)
    {
        var wishlistItems = new List<WishlistItem>();
        var used = new HashSet<string>();

        foreach (var customer in customers.Take(Math.Min(customers.Count, 18)))
        {
            var itemsForCustomer = random.Next(3, 8);

            for (var index = 0; index < itemsForCustomer; index++)
            {
                var product = products[random.Next(products.Count)];
                var key = $"{customer.Id}:{product.Id}";

                if (!used.Add(key))
                {
                    continue;
                }

                wishlistItems.Add(new WishlistItem
                {
                    Id = Guid.NewGuid(),
                    UserId = customer.Id,
                    User = customer,
                    ProductId = product.Id,
                    Product = product,
                    CreatedAt = now.AddDays(-random.Next(1, 45))
                });
            }
        }

        result.WishlistItemsCreated = wishlistItems.Count;
        return wishlistItems;
    }

    private static List<Cart> CreateActiveCarts(
        IReadOnlyList<ApplicationUser> customers,
        IReadOnlyList<Product> products,
        Random random,
        DateTime now,
        DemoSeedRunResult result)
    {
        var carts = new List<Cart>();

        foreach (var customer in customers.Take(Math.Min(customers.Count, 12)))
        {
            var cart = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                User = customer,
                Status = CartStatus.Active,
                CreatedAt = now.AddDays(-random.Next(1, 6)),
                ExpiresAt = now.AddDays(random.Next(1, 7)),
                DiscountAmount = 0m
            };

            var itemCount = random.Next(1, 5);
            var usedProducts = new HashSet<Guid>();

            for (var index = 0; index < itemCount; index++)
            {
                var product = products[random.Next(products.Count)];

                if (!usedProducts.Add(product.Id))
                {
                    continue;
                }

                var variant = product.Variants.Count > 0 && random.Next(100) < 45
                    ? product.Variants[random.Next(product.Variants.Count)]
                    : null;

                var unitPrice = variant?.PriceOverride ?? GetDemoEffectivePrice(product, now);

                cart.Items.Add(new CartItem
                {
                    Id = Guid.NewGuid(),
                    CartId = cart.Id,
                    Cart = cart,
                    ProductId = product.Id,
                    Product = product,
                    ProductVariantId = variant?.Id,
                    ProductVariant = variant,
                    Quantity = random.Next(1, 4),
                    UnitPriceSnapshot = unitPrice,
                    CreatedAt = cart.CreatedAt.AddMinutes(index * 3)
                });
            }

            carts.Add(cart);
            result.CartItemsCreated += cart.Items.Count;
        }

        result.CartsCreated = carts.Count;
        return carts;
    }

    private static WalletSeed CreateCustomerWallets(
        ApplicationUser adminUser,
        IReadOnlyList<ApplicationUser> customers,
        DateTime now,
        DemoSeedRunResult result)
    {
        var seed = new WalletSeed();

        for (var index = 0; index < Math.Min(customers.Count, 18); index++)
        {
            var customer = customers[index];
            var createdAt = now.AddDays(-30 + index);
            var creditAmount = 25000m + index * 3500m;
            var debitAmount = index % 4 == 0 ? 5000m : 0m;
            var wallet = new CustomerWallet
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                User = customer,
                Balance = creditAmount - debitAmount,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = debitAmount > 0m
                    ? createdAt.AddDays(2)
                    : createdAt
            };

            var credit = new CustomerWalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                Wallet = wallet,
                Amount = creditAmount,
                Type = CustomerWalletTransactionType.Credit,
                ReferenceType = "DemoSeed",
                Note = "Demo store credit issued for presentation.",
                CreatedAtUtc = createdAt,
                CreatedByUserId = adminUser.Id,
                CreatedByUser = adminUser
            };

            wallet.Transactions.Add(credit);
            seed.Transactions.Add(credit);

            if (debitAmount > 0m)
            {
                var debit = new CustomerWalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    Wallet = wallet,
                    Amount = debitAmount,
                    Type = CustomerWalletTransactionType.Debit,
                    ReferenceType = "DemoSeed",
                    Note = "Demo wallet debit for customer service scenario.",
                    CreatedAtUtc = createdAt.AddDays(2),
                    CreatedByUserId = adminUser.Id,
                    CreatedByUser = adminUser
                };

                wallet.Transactions.Add(debit);
                seed.Transactions.Add(debit);
            }

            seed.Wallets.Add(wallet);
        }

        result.CustomerWalletsCreated += seed.Wallets.Count;
        result.CustomerWalletTransactionsCreated += seed.Transactions.Count;

        return seed;
    }

    private static LoyaltySeed CreateLoyaltyAccounts(
        IReadOnlyList<ApplicationUser> customers,
        DateTime now,
        DemoSeedRunResult result)
    {
        var seed = new LoyaltySeed();

        for (var index = 0; index < Math.Min(customers.Count, 18); index++)
        {
            var customer = customers[index];
            var earned = 120 + index * 15;
            var redeemed = index % 5 == 0 ? 30 : 0;
            var adjusted = index % 6 == 0 ? 20 : 0;
            var account = new LoyaltyAccount
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                User = customer,
                PointsBalance = earned - redeemed + adjusted,
                LifetimeEarned = earned,
                LifetimeRedeemed = redeemed,
                CreatedAtUtc = now.AddDays(-45 + index),
                UpdatedAtUtc = now.AddDays(-5 + index % 5)
            };

            var earnedTransaction = new LoyaltyTransaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Account = account,
                Points = earned,
                Type = LoyaltyTransactionType.Earned,
                ReferenceType = "DemoSeed",
                Note = "Demo loyalty points earned from delivered orders.",
                CreatedAtUtc = account.CreatedAtUtc
            };

            account.Transactions.Add(earnedTransaction);
            seed.Transactions.Add(earnedTransaction);

            if (redeemed > 0)
            {
                var redeemedTransaction = new LoyaltyTransaction
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    Account = account,
                    Points = -redeemed,
                    Type = LoyaltyTransactionType.Redeemed,
                    ReferenceType = "DemoSeed",
                    Note = "Demo loyalty redemption scenario.",
                    CreatedAtUtc = account.CreatedAtUtc.AddDays(15)
                };

                account.Transactions.Add(redeemedTransaction);
                seed.Transactions.Add(redeemedTransaction);
            }

            if (adjusted > 0)
            {
                var adjustedTransaction = new LoyaltyTransaction
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    Account = account,
                    Points = adjusted,
                    Type = LoyaltyTransactionType.Adjusted,
                    ReferenceType = "DemoSeed",
                    Note = "Demo support adjustment.",
                    CreatedAtUtc = account.CreatedAtUtc.AddDays(20)
                };

                account.Transactions.Add(adjustedTransaction);
                seed.Transactions.Add(adjustedTransaction);
            }

            seed.Accounts.Add(account);
        }

        result.LoyaltyAccountsCreated += seed.Accounts.Count;
        result.LoyaltyTransactionsCreated += seed.Transactions.Count;

        return seed;
    }

    private static OrderSeed CreateOrders(
        IReadOnlyList<ApplicationUser> customers,
        ApplicationUser adminUser,
        ApplicationUser orderManager,
        ApplicationUser inventoryManager,
        IReadOnlyList<CustomerAddress> addresses,
        IReadOnlyList<Product> products,
        IReadOnlyList<ShippingMethod> shippingMethods,
        IReadOnlyList<Coupon> coupons,
        Random random,
        DateTime now,
        int orderCount,
        DemoSeedRunResult result)
    {
        var orderSeed = new OrderSeed();
        var reviewedUserProducts = new HashSet<string>();

        for (var orderIndex = 1; orderIndex <= orderCount; orderIndex++)
        {
            var customer = customers[random.Next(customers.Count)];
            var address = addresses.First(address => address.UserId == customer.Id && address.IsDefault);
            var shippingMethod = shippingMethods[random.Next(shippingMethods.Count)];
            var status = PickOrderStatus(random);
            var createdAt = now.AddDays(-random.Next(1, 120)).AddMinutes(-random.Next(0, 1440));
            DateTime? paidAt = IsSuccessfulPaymentStatus(status) ? createdAt.AddMinutes(random.Next(5, 120)) : null;
            DateTime? shippedAt = status is OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.ReturnRequested or OrderStatus.Returned or OrderStatus.Refunded
                ? paidAt?.AddDays(random.Next(1, 3))
                : null;
            DateTime? deliveredAt = status is OrderStatus.Delivered or OrderStatus.ReturnRequested or OrderStatus.Returned or OrderStatus.Refunded
                ? shippedAt?.AddDays(random.Next(1, 5))
                : null;
            DateTime? cancelledAt = status == OrderStatus.Cancelled ? createdAt.AddHours(random.Next(1, 24)) : null;
            var coupon = random.Next(100) < 35 ? coupons[random.Next(Math.Min(3, coupons.Count))] : null;
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"DEMO-ORD-{orderIndex:000000}",
                UserId = customer.Id,
                User = customer,
                Status = status,
                CreatedAt = createdAt,
                PaidAt = paidAt,
                ShippedAt = shippedAt,
                DeliveredAt = deliveredAt,
                CancelledAt = cancelledAt,
                CancellationReason = status == OrderStatus.Cancelled ? "Customer cancelled demo order before fulfillment." : null,
                ShippingMethodId = shippingMethod.Id,
                ShippingMethod = shippingMethod,
                ShippingMethodNameSnapshot = shippingMethod.Name,
                ShippingMethodCodeSnapshot = shippingMethod.Code,
                ShippingEstimatedDeliveryDays = shippingMethod.EstimatedDeliveryDays,
                ShippingStatus = ResolveShippingStatus(status),
                ShippingCarrier = status is OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.ReturnRequested or OrderStatus.Returned or OrderStatus.Refunded
                    ? "MATGER Demo Courier"
                    : null,
                TrackingNumber = status is OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.ReturnRequested or OrderStatus.Returned or OrderStatus.Refunded
                    ? $"DEMO-TRK-{orderIndex:000000}"
                    : null,
                DeliveryNote = status == OrderStatus.Delivered ? "Delivered to customer in demo scenario." : null,
                ShippingAddressId = address.Id,
                ShippingFullName = address.FullName,
                ShippingPhoneNumber = address.PhoneNumber,
                ShippingCountry = address.Country,
                ShippingCity = address.City,
                ShippingArea = address.Area,
                ShippingStreet = address.Street,
                ShippingBuilding = address.Building,
                ShippingFloor = address.Floor,
                ShippingApartment = address.Apartment,
                ShippingPostalCode = address.PostalCode,
                ShippingNotes = address.Notes,
                CouponId = coupon?.Id,
                Coupon = coupon
            };

            var itemCount = random.Next(1, 5);
            var usedProducts = new HashSet<Guid>();

            for (var itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                var product = products[random.Next(products.Count)];

                if (!usedProducts.Add(product.Id))
                {
                    continue;
                }

                var variant = product.Variants.Count > 0 && random.Next(100) < 55
                    ? product.Variants[random.Next(product.Variants.Count)]
                    : null;
                var unitPrice = variant?.PriceOverride ?? GetDemoEffectivePrice(product, createdAt);
                var quantity = random.Next(1, 4);
                var lineTotal = unitPrice * quantity;

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Order = order,
                    ProductId = product.Id,
                    Product = product,
                    ProductVariantId = variant?.Id,
                    ProductVariant = variant,
                    ProductNameSnapshot = product.Name,
                    ProductSkuSnapshot = product.SKU,
                    VariantNameSnapshot = variant?.Name,
                    VariantSkuSnapshot = variant?.SKU,
                    UnitPrice = unitPrice,
                    CostPriceSnapshot = product.CostPrice,
                    Quantity = quantity,
                    Total = lineTotal
                };

                order.Items.Add(orderItem);

                ApplyInventoryForOrderItem(
                    order,
                    orderItem,
                    product,
                    variant,
                    status,
                    createdAt,
                    orderSeed,
                    result);
            }

            order.Subtotal = order.Items.Sum(item => item.Total);
            order.ShippingFee = shippingMethod.BaseCost;
            order.DiscountAmount = coupon is null
                ? 0m
                : CalculateDiscount(coupon, order.Subtotal);
            order.Total = Math.Max(0m, order.Subtotal + order.ShippingFee - order.DiscountAmount);

            if (coupon is not null && order.DiscountAmount > 0m)
            {
                coupon.UsageCount++;

                var redemption = new CouponRedemption
                {
                    Id = Guid.NewGuid(),
                    CouponId = coupon.Id,
                    Coupon = coupon,
                    UserId = customer.Id,
                    User = customer,
                    OrderId = order.Id,
                    Order = order,
                    CodeSnapshot = coupon.Code,
                    DiscountAmount = order.DiscountAmount,
                    CreatedAt = createdAt.AddMinutes(2)
                };

                order.CouponRedemption = redemption;
                orderSeed.CouponRedemptions.Add(redemption);
            }

            var payment = CreatePayment(order, status, createdAt, paidAt, orderIndex);
            order.Payments.Add(payment);
            orderSeed.Payments.Add(payment);

            var paymentAttempt = CreatePaymentAttempt(payment, status, createdAt, orderIndex);

            if (paymentAttempt is not null)
            {
                payment.Attempts.Add(paymentAttempt);
                orderSeed.PaymentAttempts.Add(paymentAttempt);
            }

            var statusHistories = CreateStatusHistories(order, adminUser, orderManager, status, createdAt, paidAt, shippedAt, deliveredAt, cancelledAt);
            order.StatusHistories.AddRange(statusHistories);
            orderSeed.StatusHistories.AddRange(statusHistories);

            var internalNote = CreateInternalNote(order, adminUser, orderManager, inventoryManager, status, random);

            if (internalNote is not null)
            {
                order.InternalNotes.Add(internalNote);
                orderSeed.InternalNotes.Add(internalNote);
            }

            AddReturnAndRefundData(order, customer, adminUser, status, createdAt, deliveredAt, orderIndex, orderSeed);
            TryCreateReview(order, customer, adminUser, random, reviewedUserProducts, orderSeed);

            orderSeed.Orders.Add(order);
        }

        result.OrdersCreated = orderSeed.Orders.Count;
        result.OrderItemsCreated = orderSeed.Orders.Sum(order => order.Items.Count);
        result.PaymentsCreated = orderSeed.Payments.Count;
        result.PaymentAttemptsCreated = orderSeed.PaymentAttempts.Count;
        result.InventoryReservationsCreated = orderSeed.InventoryReservations.Count;
        result.CouponRedemptionsCreated = orderSeed.CouponRedemptions.Count;
        result.ReturnRequestsCreated = orderSeed.ReturnRequests.Count;
        result.RefundsCreated = orderSeed.Refunds.Count;
        result.ProductReviewsCreated = orderSeed.ProductReviews.Count;
        result.OrderStatusHistoriesCreated = orderSeed.StatusHistories.Count;
        result.OrderInternalNotesCreated = orderSeed.InternalNotes.Count;
        result.InventoryMovementsCreated += orderSeed.InventoryMovements.Count;

        return orderSeed;
    }

    private static void ApplyInventoryForOrderItem(
        Order order,
        OrderItem orderItem,
        Product product,
        ProductVariant? variant,
        OrderStatus status,
        DateTime createdAt,
        OrderSeed orderSeed,
        DemoSeedRunResult result)
    {
        var reservation = new InventoryReservation
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            ProductId = product.Id,
            Product = product,
            ProductVariantId = variant?.Id,
            ProductVariant = variant,
            Quantity = orderItem.Quantity,
            Status = ResolveReservationStatus(status),
            CreatedAt = createdAt.AddMinutes(1),
            ExpiresAt = status == OrderStatus.PendingPayment
                ? DateTime.UtcNow.AddMinutes(30)
                : createdAt.AddMinutes(16)
        };

        if (reservation.Status == InventoryReservationStatus.Confirmed)
        {
            reservation.ConfirmedAt = createdAt.AddMinutes(7);
        }
        else if (reservation.Status == InventoryReservationStatus.Released)
        {
            reservation.ReleasedAt = createdAt.AddMinutes(20);
        }
        else if (reservation.Status == InventoryReservationStatus.Expired)
        {
            reservation.ExpiredAt = createdAt.AddMinutes(20);
        }

        order.InventoryReservations.Add(reservation);
        orderSeed.InventoryReservations.Add(reservation);

        AddReservationCreatedMovement(product, variant, reservation, createdAt, orderSeed);

        if (!IsSuccessfulPaymentStatus(status))
        {
            AddReservationReleaseMovement(product, variant, reservation, status, createdAt, orderSeed);
            return;
        }

        var restocked = status is OrderStatus.Returned or OrderStatus.Refunded;
        ApplySaleMovement(product, variant, orderItem.Quantity, order.OrderNumber, createdAt.AddMinutes(8), orderSeed);

        if (restocked)
        {
            ApplyReturnMovement(product, variant, orderItem.Quantity, order.OrderNumber, createdAt.AddDays(5), orderSeed);
        }
    }

    private static void AddReservationCreatedMovement(
        Product product,
        ProductVariant? variant,
        InventoryReservation reservation,
        DateTime createdAt,
        OrderSeed orderSeed)
    {
        var beforeAvailable = GetQuantityAvailable(product, variant);
        var beforeReserved = GetQuantityReserved(product, variant);

        SetQuantityReserved(product, variant, beforeReserved + reservation.Quantity);

        orderSeed.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            InventoryItemId = variant is null ? product.InventoryItem?.Id : null,
            ProductVariantId = variant?.Id,
            Type = InventoryMovementType.ReservationCreated,
            QuantityChange = reservation.Quantity,
            QuantityAvailableBefore = beforeAvailable,
            QuantityAvailableAfter = beforeAvailable,
            QuantityReservedBefore = beforeReserved,
            QuantityReservedAfter = beforeReserved + reservation.Quantity,
            Reason = "Demo checkout reservation created.",
            ReferenceType = "Order",
            ReferenceId = reservation.Order.OrderNumber,
            CreatedAt = createdAt.AddMinutes(1)
        });
    }

    private static void AddReservationReleaseMovement(
        Product product,
        ProductVariant? variant,
        InventoryReservation reservation,
        OrderStatus status,
        DateTime createdAt,
        OrderSeed orderSeed)
    {
        var beforeAvailable = GetQuantityAvailable(product, variant);
        var beforeReserved = GetQuantityReserved(product, variant);
        var afterReserved = Math.Max(0, beforeReserved - reservation.Quantity);

        SetQuantityReserved(product, variant, afterReserved);

        var movementType = status == OrderStatus.PaymentFailed
            ? InventoryMovementType.ReservationReleased
            : InventoryMovementType.ReservationExpired;

        orderSeed.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            InventoryItemId = variant is null ? product.InventoryItem?.Id : null,
            ProductVariantId = variant?.Id,
            Type = movementType,
            QuantityChange = -reservation.Quantity,
            QuantityAvailableBefore = beforeAvailable,
            QuantityAvailableAfter = beforeAvailable,
            QuantityReservedBefore = beforeReserved,
            QuantityReservedAfter = afterReserved,
            Reason = status == OrderStatus.PaymentFailed
                ? "Demo payment failed and reservation was released."
                : "Demo reservation was cancelled or expired.",
            ReferenceType = "Order",
            ReferenceId = reservation.Order.OrderNumber,
            CreatedAt = createdAt.AddMinutes(20)
        });
    }

    private static void ApplySaleMovement(
        Product product,
        ProductVariant? variant,
        int quantity,
        string orderNumber,
        DateTime createdAt,
        OrderSeed orderSeed)
    {
        var beforeAvailable = GetQuantityAvailable(product, variant);
        var beforeReserved = GetQuantityReserved(product, variant);
        var afterAvailable = Math.Max(0, beforeAvailable - quantity);
        var afterReserved = Math.Max(0, beforeReserved - quantity);

        SetQuantityAvailable(product, variant, afterAvailable);
        SetQuantityReserved(product, variant, afterReserved);

        orderSeed.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            InventoryItemId = variant is null ? product.InventoryItem?.Id : null,
            ProductVariantId = variant?.Id,
            Type = InventoryMovementType.SaleConfirmed,
            QuantityChange = -quantity,
            QuantityAvailableBefore = beforeAvailable,
            QuantityAvailableAfter = afterAvailable,
            QuantityReservedBefore = beforeReserved,
            QuantityReservedAfter = afterReserved,
            Reason = "Demo paid order consumed inventory.",
            ReferenceType = "Order",
            ReferenceId = orderNumber,
            CreatedAt = createdAt
        });
    }

    private static void ApplyReturnMovement(
        Product product,
        ProductVariant? variant,
        int quantity,
        string orderNumber,
        DateTime createdAt,
        OrderSeed orderSeed)
    {
        var beforeAvailable = GetQuantityAvailable(product, variant);
        var beforeReserved = GetQuantityReserved(product, variant);
        var afterAvailable = beforeAvailable + quantity;

        SetQuantityAvailable(product, variant, afterAvailable);

        orderSeed.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            InventoryItemId = variant is null ? product.InventoryItem?.Id : null,
            ProductVariantId = variant?.Id,
            Type = InventoryMovementType.ReturnRestocked,
            QuantityChange = quantity,
            QuantityAvailableBefore = beforeAvailable,
            QuantityAvailableAfter = afterAvailable,
            QuantityReservedBefore = beforeReserved,
            QuantityReservedAfter = beforeReserved,
            Reason = "Demo returned order restocked inventory.",
            ReferenceType = "Order",
            ReferenceId = orderNumber,
            CreatedAt = createdAt
        });
    }

    private static Payment CreatePayment(
        Order order,
        OrderStatus status,
        DateTime createdAt,
        DateTime? paidAt,
        int orderIndex)
    {
        var paymentStatus = status switch
        {
            OrderStatus.PendingPayment => PaymentStatus.Pending,
            OrderStatus.PaymentFailed or OrderStatus.Cancelled => PaymentStatus.Failed,
            OrderStatus.Refunded => PaymentStatus.Refunded,
            _ => PaymentStatus.Succeeded
        };

        return new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            Amount = order.Total,
            Status = paymentStatus,
            ProviderReference = $"DEMO-PAY-{orderIndex:000000}",
            CreatedAt = createdAt.AddMinutes(3),
            ConfirmedAt = paymentStatus is PaymentStatus.Succeeded or PaymentStatus.Refunded ? paidAt : null,
            FailedAt = paymentStatus == PaymentStatus.Failed ? createdAt.AddMinutes(15) : null
        };
    }

    private static PaymentAttempt? CreatePaymentAttempt(
        Payment payment,
        OrderStatus status,
        DateTime createdAt,
        int orderIndex)
    {
        if (status == OrderStatus.PendingPayment)
        {
            return null;
        }

        return new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Payment = payment,
            AttemptNumber = 1,
            Status = payment.Status == PaymentStatus.Failed
                ? PaymentAttemptStatus.Failed
                : PaymentAttemptStatus.Succeeded,
            FailureReason = payment.Status == PaymentStatus.Failed
                ? $"Demo failed attempt for order {orderIndex:000000}."
                : null,
            CreatedAt = createdAt.AddMinutes(5)
        };
    }

    private static List<OrderStatusHistory> CreateStatusHistories(
        Order order,
        ApplicationUser adminUser,
        ApplicationUser orderManager,
        OrderStatus finalStatus,
        DateTime createdAt,
        DateTime? paidAt,
        DateTime? shippedAt,
        DateTime? deliveredAt,
        DateTime? cancelledAt)
    {
        var histories = new List<OrderStatusHistory>
        {
            NewHistory(order, null, OrderStatus.PendingPayment, null, "Demo checkout created pending payment order.", createdAt)
        };

        if (finalStatus == OrderStatus.PendingPayment)
        {
            return histories;
        }

        if (finalStatus == OrderStatus.PaymentFailed)
        {
            histories.Add(NewHistory(order, OrderStatus.PendingPayment, OrderStatus.PaymentFailed, null, "Demo payment failure.", createdAt.AddMinutes(15)));
            return histories;
        }

        if (finalStatus == OrderStatus.Cancelled)
        {
            histories.Add(NewHistory(order, OrderStatus.PendingPayment, OrderStatus.Cancelled, orderManager.Id, "Demo order cancelled.", cancelledAt ?? createdAt.AddHours(1)));
            return histories;
        }

        histories.Add(NewHistory(order, OrderStatus.PendingPayment, OrderStatus.Paid, null, "Demo payment confirmed.", paidAt ?? createdAt.AddMinutes(10)));

        if (finalStatus == OrderStatus.Paid)
        {
            return histories;
        }

        histories.Add(NewHistory(order, OrderStatus.Paid, OrderStatus.Processing, orderManager.Id, "Demo order moved to fulfillment.", paidAt?.AddHours(2) ?? createdAt.AddHours(2)));

        if (finalStatus == OrderStatus.Processing)
        {
            return histories;
        }

        histories.Add(NewHistory(order, OrderStatus.Processing, OrderStatus.Shipped, orderManager.Id, "Demo shipment dispatched.", shippedAt ?? createdAt.AddDays(1)));

        if (finalStatus == OrderStatus.Shipped)
        {
            return histories;
        }

        histories.Add(NewHistory(order, OrderStatus.Shipped, OrderStatus.Delivered, orderManager.Id, "Demo order delivered.", deliveredAt ?? createdAt.AddDays(3)));

        if (finalStatus == OrderStatus.Delivered)
        {
            return histories;
        }

        histories.Add(NewHistory(order, OrderStatus.Delivered, OrderStatus.ReturnRequested, adminUser.Id, "Demo customer opened a return request.", (deliveredAt ?? createdAt.AddDays(3)).AddDays(1)));

        if (finalStatus == OrderStatus.ReturnRequested)
        {
            return histories;
        }

        histories.Add(NewHistory(order, OrderStatus.ReturnRequested, OrderStatus.Returned, adminUser.Id, "Demo return completed and stock inspected.", (deliveredAt ?? createdAt.AddDays(3)).AddDays(4)));

        if (finalStatus == OrderStatus.Returned)
        {
            return histories;
        }

        histories.Add(NewHistory(order, OrderStatus.Returned, OrderStatus.Refunded, adminUser.Id, "Demo refund completed.", (deliveredAt ?? createdAt.AddDays(3)).AddDays(5)));

        return histories;
    }

    private static OrderStatusHistory NewHistory(
        Order order,
        OrderStatus? previousStatus,
        OrderStatus newStatus,
        Guid? changedByUserId,
        string reason,
        DateTime createdAt)
    {
        return new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId,
            Reason = reason,
            Note = $"Auto-generated demo history for {order.OrderNumber}.",
            CreatedAt = createdAt
        };
    }

    private static OrderInternalNote? CreateInternalNote(
        Order order,
        ApplicationUser adminUser,
        ApplicationUser orderManager,
        ApplicationUser inventoryManager,
        OrderStatus status,
        Random random)
    {
        if (random.Next(100) > 45 && order.Total < 500000m && status is not (OrderStatus.ReturnRequested or OrderStatus.Returned or OrderStatus.Refunded))
        {
            return null;
        }

        var author = status switch
        {
            OrderStatus.Processing or OrderStatus.Shipped => orderManager,
            OrderStatus.ReturnRequested or OrderStatus.Returned or OrderStatus.Refunded => adminUser,
            _ => inventoryManager
        };

        return new OrderInternalNote
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            AuthorUserId = author.Id,
            AuthorUser = author,
            Note = status switch
            {
                OrderStatus.ReturnRequested or OrderStatus.Returned or OrderStatus.Refunded => "Demo internal note: return/refund order requires careful inventory and payment review.",
                OrderStatus.Shipped => "Demo internal note: shipment dispatched, monitor courier SLA.",
                _ => "Demo internal note: high-value or operationally relevant order."
            },
            CreatedAt = order.CreatedAt.AddHours(3)
        };
    }

    private static void AddReturnAndRefundData(
        Order order,
        ApplicationUser customer,
        ApplicationUser adminUser,
        OrderStatus status,
        DateTime createdAt,
        DateTime? deliveredAt,
        int orderIndex,
        OrderSeed orderSeed)
    {
        if (status is not (OrderStatus.ReturnRequested or OrderStatus.Returned or OrderStatus.Refunded))
        {
            return;
        }

        var requestedAt = (deliveredAt ?? createdAt.AddDays(3)).AddDays(1);
        var returnRequest = new ReturnRequest
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            UserId = customer.Id,
            User = customer,
            Reason = "Demo customer return request: product did not match expectation.",
            Status = status switch
            {
                OrderStatus.ReturnRequested => ReturnRequestStatus.Requested,
                OrderStatus.Returned or OrderStatus.Refunded => ReturnRequestStatus.Completed,
                _ => ReturnRequestStatus.Requested
            },
            AdminNote = status == OrderStatus.ReturnRequested
                ? "Demo return awaiting admin review."
                : "Demo return completed after inspection.",
            RequestedAt = requestedAt,
            ApprovedAt = status is OrderStatus.Returned or OrderStatus.Refunded ? requestedAt.AddHours(6) : null,
            CompletedAt = status is OrderStatus.Returned or OrderStatus.Refunded ? requestedAt.AddDays(3) : null
        };

        order.ReturnRequests.Add(returnRequest);
        orderSeed.ReturnRequests.Add(returnRequest);

        if (status != OrderStatus.Refunded)
        {
            return;
        }

        var refund = new Refund
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Order = order,
            Amount = Math.Min(order.Total, order.Subtotal),
            Reason = "Demo refund after completed return.",
            Status = RefundStatus.Completed,
            ProviderReference = $"DEMO-REF-{orderIndex:000000}",
            CreatedAt = requestedAt.AddDays(3).AddHours(1),
            CompletedAt = requestedAt.AddDays(3).AddHours(2)
        };

        order.Refunds.Add(refund);
        orderSeed.Refunds.Add(refund);
    }

    private static void TryCreateReview(
        Order order,
        ApplicationUser customer,
        ApplicationUser adminUser,
        Random random,
        HashSet<string> reviewedUserProducts,
        OrderSeed orderSeed)
    {
        if (order.Status is not (OrderStatus.Delivered or OrderStatus.ReturnRequested or OrderStatus.Returned or OrderStatus.Refunded))
        {
            return;
        }

        if (order.Items.Count == 0 || random.Next(100) > 72)
        {
            return;
        }

        var item = order.Items[random.Next(order.Items.Count)];
        var reviewKey = $"{customer.Id}:{item.ProductId}";

        if (!reviewedUserProducts.Add(reviewKey))
        {
            return;
        }

        var rating = random.Next(100) switch
        {
            < 8 => 2,
            < 25 => 3,
            < 60 => 4,
            _ => 5
        };

        var status = random.Next(100) switch
        {
            < 6 => ProductReviewStatus.Pending,
            < 10 => ProductReviewStatus.Hidden,
            _ => ProductReviewStatus.Visible
        };

        var review = new ProductReview
        {
            Id = Guid.NewGuid(),
            UserId = customer.Id,
            User = customer,
            ProductId = item.ProductId,
            Product = item.Product,
            OrderId = order.Id,
            Order = order,
            Rating = rating,
            Comment = rating >= 4
                ? "Demo review: good product quality and acceptable delivery experience."
                : "Demo review: product was acceptable but needs better packaging.",
            Status = status,
            CreatedAt = (order.DeliveredAt ?? order.CreatedAt).AddDays(random.Next(1, 12)),
            HiddenAt = status == ProductReviewStatus.Hidden ? DateTime.UtcNow.AddDays(-random.Next(1, 20)) : null,
            HiddenByUserId = status == ProductReviewStatus.Hidden ? adminUser.Id : null,
            HiddenByUser = status == ProductReviewStatus.Hidden ? adminUser : null,
            AdminNote = status == ProductReviewStatus.Hidden ? "Demo moderation note: hidden for internal presentation." : null
        };

        orderSeed.ProductReviews.Add(review);
    }

    private static StockAdjustmentSeed CreateStockAdjustmentRequests(
        ApplicationUser adminUser,
        ApplicationUser inventoryManager,
        IReadOnlyList<Product> products,
        DateTime now,
        DemoSeedRunResult result)
    {
        var seed = new StockAdjustmentSeed();
        var candidates = products
            .Where(product => product.InventoryItem is not null)
            .Take(6)
            .ToList();

        for (var index = 0; index < candidates.Count; index++)
        {
            var product = candidates[index];
            var inventoryItem = product.InventoryItem!;
            var requestedAt = now.AddDays(-12 + index);

            var request = new StockAdjustmentRequest
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Product = product,
                RequestedByUserId = inventoryManager.Id,
                QuantityChange = index % 2 == 0 ? 12 + index : -Math.Min(5, Math.Max(1, inventoryItem.QuantityAvailable / 10)),
                Reason = "Demo inventory count correction request.",
                RequestedAtUtc = requestedAt,
                Status = index switch
                {
                    0 or 1 => StockAdjustmentRequestStatus.Approved,
                    2 or 3 => StockAdjustmentRequestStatus.Rejected,
                    _ => StockAdjustmentRequestStatus.Pending
                }
            };

            if (request.Status == StockAdjustmentRequestStatus.Approved)
            {
                var beforeAvailable = inventoryItem.QuantityAvailable;
                var afterAvailable = Math.Max(0, beforeAvailable + request.QuantityChange);
                request.QuantityChange = afterAvailable - beforeAvailable;
                inventoryItem.QuantityAvailable = afterAvailable;
                inventoryItem.UpdatedAt = requestedAt.AddHours(2);

                var movement = new InventoryMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    InventoryItemId = inventoryItem.Id,
                    Type = InventoryMovementType.ManualAdjustment,
                    QuantityChange = request.QuantityChange,
                    QuantityAvailableBefore = beforeAvailable,
                    QuantityAvailableAfter = afterAvailable,
                    QuantityReservedBefore = inventoryItem.QuantityReserved,
                    QuantityReservedAfter = inventoryItem.QuantityReserved,
                    Reason = "Demo approved stock adjustment request.",
                    ReferenceType = nameof(StockAdjustmentRequest),
                    ReferenceId = request.Id.ToString(),
                    ActorUserId = adminUser.Id,
                    CreatedAt = requestedAt.AddHours(2)
                };

                request.ReviewedByUserId = adminUser.Id;
                request.ReviewedAtUtc = movement.CreatedAt;
                request.ReviewNote = "Demo request approved after warehouse count.";
                request.AppliedInventoryMovementId = movement.Id;
                request.AppliedInventoryMovement = movement;

                seed.InventoryMovements.Add(movement);
            }
            else if (request.Status == StockAdjustmentRequestStatus.Rejected)
            {
                request.ReviewedByUserId = adminUser.Id;
                request.ReviewedAtUtc = requestedAt.AddHours(3);
                request.ReviewNote = "Demo request rejected after recount.";
            }

            seed.Requests.Add(request);
        }

        result.StockAdjustmentRequestsCreated += seed.Requests.Count;
        result.InventoryMovementsCreated += seed.InventoryMovements.Count;

        return seed;
    }

    private static List<CustomerInternalNote> CreateCustomerInternalNotes(
        ApplicationUser adminUser,
        ApplicationUser orderManager,
        IReadOnlyList<ApplicationUser> customers,
        DateTime now,
        DemoSeedRunResult result)
    {
        var notes = new List<CustomerInternalNote>();
        var selectedCustomers = customers
            .Take(Math.Min(customers.Count, 12))
            .ToList();

        for (var index = 0; index < selectedCustomers.Count; index++)
        {
            var customer = selectedCustomers[index];
            var createdBy = index % 3 == 0 ? adminUser : orderManager;
            var scenario = index switch
            {
                0 => "VIP candidate: high lifetime value and fast fulfillment preference.",
                1 => "Dormant customer: last engagement needs follow-up during demo campaign.",
                2 => "High refund watch: review future return requests before approving.",
                3 => "Active buyer: responds well to electronics and accessories bundles.",
                4 => "New customer: first orders should receive careful delivery follow-up.",
                _ => "Demo customer profile note for admin-only Customer 360 review."
            };

            notes.Add(new CustomerInternalNote
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Customer = customer,
                CreatedByUserId = createdBy.Id,
                CreatedByUser = createdBy,
                Note = scenario,
                CreatedAtUtc = now.AddDays(-20 + index).AddHours(index),
                IsImportant = index is 0 or 2 or 5
            });
        }

        result.CustomerInternalNotesCreated += notes.Count;
        return notes;
    }

    private static List<RiskSignal> CreateRiskSignals(
        ApplicationUser adminUser,
        IReadOnlyList<Order> orders,
        DateTime now,
        DemoSeedRunResult result)
    {
        var signalTypes = new[]
        {
            "NewCustomerHighValueOrder",
            "HighOrderFrequency24h",
            "HighRefundRatio",
            "NewShippingAddressHighValue",
            "SuspiciousQuantity",
            "RepeatedFailedPayments",
            "CouponAbuse"
        };
        var severities = new[]
        {
            RiskSignalSeverity.Critical,
            RiskSignalSeverity.High,
            RiskSignalSeverity.Medium,
            RiskSignalSeverity.Low
        };
        var signals = new List<RiskSignal>();

        for (var index = 0; index < Math.Min(orders.Count, 14); index++)
        {
            var order = orders[index];
            var status = index switch
            {
                < 7 => RiskSignalStatus.Open,
                < 11 => RiskSignalStatus.Resolved,
                _ => RiskSignalStatus.Dismissed
            };
            var createdAt = now.AddDays(-10 + index).AddHours(index);
            var signal = new RiskSignal
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Order = order,
                UserId = order.UserId,
                User = order.User,
                SignalType = signalTypes[index % signalTypes.Length],
                Severity = severities[index % severities.Length],
                Details = "Demo risk signal for admin review workflow.",
                CreatedAtUtc = createdAt,
                Status = status
            };

            if (status != RiskSignalStatus.Open)
            {
                signal.ReviewedByUserId = adminUser.Id;
                signal.ReviewedByUser = adminUser;
                signal.ReviewedAtUtc = createdAt.AddHours(6);
                signal.ResolutionNote = status == RiskSignalStatus.Resolved
                    ? "Demo signal resolved after admin review."
                    : "Demo signal dismissed as acceptable risk.";
            }

            signals.Add(signal);
        }

        result.RiskSignalsCreated += signals.Count;
        return signals;
    }

    private static List<AuditLog> CreateAuditLogs(
        ApplicationUser adminUser,
        ApplicationUser orderManager,
        ApplicationUser inventoryManager,
        IReadOnlyList<Product> products,
        IReadOnlyList<Order> orders,
        DateTime now,
        DemoSeedRunResult result)
    {
        var auditLogs = new List<AuditLog>
        {
            new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = adminUser.Id,
                Action = "DemoSeed.Completed",
                EntityName = "DemoData",
                EntityId = "MATGER-PASS2",
                Reason = "Large demo seed data created for MATGER presentation.",
                CreatedAt = now
            }
        };

        foreach (var product in products.Take(20))
        {
            auditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = inventoryManager.Id,
                Action = "Inventory.DemoBalanceReviewed",
                EntityName = nameof(Product),
                EntityId = product.Id.ToString(),
                NewValueJson = $"{{\"sku\":\"{product.SKU}\",\"name\":\"{product.Name}\"}}",
                Reason = "Demo inventory review event.",
                CreatedAt = product.CreatedAt.AddDays(1)
            });
        }

        foreach (var order in orders.Take(35))
        {
            auditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = orderManager.Id,
                Action = "Order.DemoOperationalReview",
                EntityName = nameof(Order),
                EntityId = order.Id.ToString(),
                NewValueJson = $"{{\"orderNumber\":\"{order.OrderNumber}\",\"status\":\"{order.Status}\"}}",
                Reason = "Demo order operational review event.",
                CreatedAt = order.CreatedAt.AddHours(4)
            });
        }

        result.AuditLogsCreated = auditLogs.Count;
        return auditLogs;
    }

    private static DemoSeedOptions NormalizeOptions(DemoSeedOptions options)
    {
        options.CustomerCount = Math.Clamp(options.CustomerCount, 6, 80);
        options.ProductsPerCategory = Math.Clamp(options.ProductsPerCategory, 6, 20);
        options.OrderCount = Math.Clamp(options.OrderCount, 40, 1000);

        if (string.IsNullOrWhiteSpace(options.DemoPassword))
        {
            options.DemoPassword = "Demo12345";
        }

        if (options.DemoPassword.Length < 8)
        {
            options.DemoPassword = "Demo12345";
        }

        return options;
    }

    private static string FormatIdentityErrors(IdentityResult result)
    {
        return string.Join("; ", result.Errors.Select(error => error.Description));
    }

    private static bool ShouldCreateVariants(CategorySpec categorySpec, int index)
    {
        return categorySpec.Slug.Contains("fashion", StringComparison.OrdinalIgnoreCase) ||
               categorySpec.Slug.Contains("computers", StringComparison.OrdinalIgnoreCase) ||
               categorySpec.Slug.Contains("gaming", StringComparison.OrdinalIgnoreCase) ||
               index % 2 == 0;
    }

    private static string[] GetVariantNames(string slug, int index)
    {
        if (slug.Contains("fashion", StringComparison.OrdinalIgnoreCase))
        {
            return index % 2 == 0
                ? ["Size M - Black", "Size L - Navy", "Size XL - White"]
                : ["Small", "Medium", "Large"];
        }

        if (slug.Contains("computers", StringComparison.OrdinalIgnoreCase) || slug.Contains("gaming", StringComparison.OrdinalIgnoreCase))
        {
            return ["Standard", "Pro", "Max"];
        }

        if (slug.Contains("electronics", StringComparison.OrdinalIgnoreCase))
        {
            return ["64GB", "128GB", "256GB"];
        }

        return ["Single Pack", "Bundle Pack"];
    }

    private static OrderStatus PickOrderStatus(Random random)
    {
        return random.Next(100) switch
        {
            < 7 => OrderStatus.PendingPayment,
            < 11 => OrderStatus.PaymentFailed,
            < 20 => OrderStatus.Paid,
            < 32 => OrderStatus.Processing,
            < 45 => OrderStatus.Shipped,
            < 76 => OrderStatus.Delivered,
            < 82 => OrderStatus.Cancelled,
            < 90 => OrderStatus.ReturnRequested,
            < 96 => OrderStatus.Returned,
            _ => OrderStatus.Refunded
        };
    }

    private static bool IsSuccessfulPaymentStatus(OrderStatus status)
    {
        return status is OrderStatus.Paid
            or OrderStatus.Processing
            or OrderStatus.Shipped
            or OrderStatus.Delivered
            or OrderStatus.ReturnRequested
            or OrderStatus.Returned
            or OrderStatus.Refunded;
    }

    private static ShippingStatus ResolveShippingStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.PendingPayment or OrderStatus.Paid or OrderStatus.PaymentFailed => ShippingStatus.Pending,
            OrderStatus.Processing => ShippingStatus.ReadyToShip,
            OrderStatus.Shipped => ShippingStatus.Shipped,
            OrderStatus.Delivered or OrderStatus.ReturnRequested => ShippingStatus.Delivered,
            OrderStatus.Returned or OrderStatus.Refunded => ShippingStatus.Returned,
            OrderStatus.Cancelled => ShippingStatus.Failed,
            _ => ShippingStatus.Pending
        };
    }

    private static InventoryReservationStatus ResolveReservationStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.PendingPayment => InventoryReservationStatus.Pending,
            OrderStatus.PaymentFailed => InventoryReservationStatus.Released,
            OrderStatus.Cancelled => InventoryReservationStatus.Expired,
            _ => InventoryReservationStatus.Confirmed
        };
    }

    private static decimal CalculateDiscount(Coupon coupon, decimal subtotal)
    {
        if (subtotal < coupon.MinimumOrderSubtotal)
        {
            return 0m;
        }

        var discount = coupon.DiscountType == CouponDiscountType.Percentage
            ? subtotal * (coupon.DiscountValue / 100m)
            : coupon.DiscountValue;

        if (coupon.MaxDiscountAmount.HasValue)
        {
            discount = Math.Min(discount, coupon.MaxDiscountAmount.Value);
        }

        return Money(discount);
    }

    private static decimal RandomDecimal(Random random, decimal min, decimal max)
    {
        return min + (decimal)random.NextDouble() * (max - min);
    }

    private static decimal Money(decimal value, int decimals = 2)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    private static int GetQuantityAvailable(Product product, ProductVariant? variant)
    {
        return variant?.QuantityAvailable ?? product.InventoryItem?.QuantityAvailable ?? 0;
    }

    private static int GetQuantityReserved(Product product, ProductVariant? variant)
    {
        return variant?.QuantityReserved ?? product.InventoryItem?.QuantityReserved ?? 0;
    }

    private static void SetQuantityAvailable(Product product, ProductVariant? variant, int value)
    {
        if (variant is not null)
        {
            variant.QuantityAvailable = value;
            variant.UpdatedAt = DateTime.UtcNow;
            return;
        }

        if (product.InventoryItem is not null)
        {
            product.InventoryItem.QuantityAvailable = value;
            product.InventoryItem.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static void SetQuantityReserved(Product product, ProductVariant? variant, int value)
    {
        if (variant is not null)
        {
            variant.QuantityReserved = value;
            variant.UpdatedAt = DateTime.UtcNow;
            return;
        }

        if (product.InventoryItem is not null)
        {
            product.InventoryItem.QuantityReserved = value;
            product.InventoryItem.UpdatedAt = DateTime.UtcNow;
        }
    }

    private sealed record BrandSpec(
        string Name,
        string Slug);

    private sealed record CategorySpec(
        string Name,
        string Slug,
        string Code,
        decimal MinPrice,
        decimal MaxPrice,
        string[] ProductNames);

    private sealed record CatalogSeed(
        List<Product> Products,
        List<InventoryItem> InventoryItems,
        List<InventoryMovement> InitialInventoryMovements);

    private sealed class StockAdjustmentSeed
    {
        public List<StockAdjustmentRequest> Requests { get; } = [];

        public List<InventoryMovement> InventoryMovements { get; } = [];
    }

    private sealed class WalletSeed
    {
        public List<CustomerWallet> Wallets { get; } = [];

        public List<CustomerWalletTransaction> Transactions { get; } = [];
    }

    private sealed class LoyaltySeed
    {
        public List<LoyaltyAccount> Accounts { get; } = [];

        public List<LoyaltyTransaction> Transactions { get; } = [];
    }

    private sealed class OrderSeed
    {
        public List<Order> Orders { get; } = [];

        public List<Payment> Payments { get; } = [];

        public List<PaymentAttempt> PaymentAttempts { get; } = [];

        public List<InventoryReservation> InventoryReservations { get; } = [];

        public List<CouponRedemption> CouponRedemptions { get; } = [];

        public List<ReturnRequest> ReturnRequests { get; } = [];

        public List<Refund> Refunds { get; } = [];

        public List<ProductReview> ProductReviews { get; } = [];

        public List<OrderStatusHistory> StatusHistories { get; } = [];

        public List<OrderInternalNote> InternalNotes { get; } = [];

        public List<InventoryMovement> InventoryMovements { get; } = [];
    }

    private static readonly string[] DemoCustomerNames =
    [
        "Ahmed Ali", "Sara Hassan", "Mustafa Kareem", "Noor Abbas", "Ali Hussein", "Zainab Qasim",
        "Omar Saad", "Mariam Naser", "Hussein Raad", "Rania Mahdi", "Yousif Sami", "Fatima Adel",
        "Karrar Jawad", "Alaa Firas", "Dalia Sabah", "Mohammed Faris", "Hiba Laith", "Ammar Hadi",
        "Mina Qutaiba", "Hassan Saleh", "Ruqaya Raad", "Ibrahim Khalid", "Lina Waleed", "Bilal Samer",
        "Murtadha Amir", "Aseel Hameed", "Haider Nabil", "Aya Ziad", "Nour Ali", "Saif Raad"
    ];

    private static readonly BrandSpec[] DemoBrands =
    [
        new BrandSpec("NovaTech", "novatech"),
        new BrandSpec("UrbanCart", "urbancart"),
        new BrandSpec("HomePro", "homepro"),
        new BrandSpec("FitCore", "fitcore"),
        new BrandSpec("GlowLab", "glowlab"),
        new BrandSpec("ByteForge", "byteforge"),
        new BrandSpec("RoadMate", "roadmate"),
        new BrandSpec("PaperMint", "papermint"),
        new BrandSpec("GamePulse", "gamepulse"),
        new BrandSpec("StyleAxis", "styleaxis")
    ];

    private static readonly string[] DemoSuppliers =
    [
        "Baghdad Wholesale Hub",
        "Basra Trade Supply",
        "Erbil Commercial Group",
        "Levant Distribution",
        "Gulf Retail Partners",
        "Nahrain Logistics"
    ];

    private static readonly CategorySpec[] DemoCategories =
    [
        new CategorySpec(
            "Demo Electronics",
            "demo-electronics",
            "ELC",
            45000m,
            650000m,
            [
                "Wireless Noise Cancelling Headphones", "Smart Fitness Watch", "Portable Bluetooth Speaker",
                "USB-C Fast Charger", "Power Bank 20000mAh", "Smart Home Camera",
                "Wireless Earbuds Pro", "HD Streaming Stick", "Digital Drawing Tablet", "Portable Mini Projector",
                "Smart LED Light Kit", "Car Bluetooth Adapter", "Compact Action Camera", "Travel Power Adapter",
                "Wireless Charging Pad", "Smart Door Sensor", "Laptop Cooling Stand", "Premium HDMI Cable",
                "Rechargeable Desk Lamp", "Noise Reduction Microphone"
            ]),
        new CategorySpec(
            "Demo Mobile Accessories",
            "demo-mobile-accessories",
            "MOB",
            8000m,
            120000m,
            [
                "MagSafe Clear Case", "Tempered Glass Protector", "Phone Camera Lens Kit", "Foldable Phone Stand",
                "Braided USB-C Cable", "Car Phone Holder", "Gaming Phone Trigger", "Waterproof Phone Pouch",
                "Magnetic Ring Holder", "Dual Port Car Charger", "Silicone Protective Case", "Universal SIM Tool Kit",
                "Phone Cleaning Kit", "Metal Tablet Stand", "Selfie Tripod Remote", "Wireless Lavalier Mic",
                "Phone Cooling Fan", "Anti Slip Dashboard Mat", "Tablet Sleeve 11 Inch", "Compact Cable Organizer"
            ]),
        new CategorySpec(
            "Demo Computers",
            "demo-computers",
            "CMP",
            55000m,
            1800000m,
            [
                "Mechanical Keyboard", "Ergonomic Wireless Mouse", "27 Inch IPS Monitor", "NVMe SSD 1TB",
                "DDR5 Memory Kit", "USB-C Docking Station", "Laptop Backpack Pro", "External HDD 2TB",
                "Wi-Fi 6 Router", "Webcam Full HD", "Laptop Stand Aluminum", "Graphics Tablet Medium",
                "Gaming Mouse Pad XXL", "Portable USB Monitor", "Mini PC Office Edition", "All-in-One Printer",
                "Network Switch 8 Port", "UPS Backup 1200VA", "Thermal Paste Kit", "PC Cleaning Air Blower"
            ]),
        new CategorySpec(
            "Demo Home Appliances",
            "demo-home-appliances",
            "HOM",
            25000m,
            950000m,
            [
                "Air Fryer 6L", "Smart Electric Kettle", "Robot Vacuum Cleaner", "Steam Iron Pro",
                "Mini Espresso Machine", "Digital Kitchen Scale", "Ceramic Heater", "Water Dispenser",
                "Handheld Vacuum", "Rice Cooker", "Stand Mixer", "Portable Blender",
                "Air Purifier", "Induction Cooker", "Food Processor", "Electric Grill",
                "Garment Steamer", "Microwave Oven", "Dehumidifier", "Cordless Mop"
            ]),
        new CategorySpec(
            "Demo Fashion",
            "demo-fashion",
            "FSH",
            15000m,
            180000m,
            [
                "Classic Cotton T-Shirt", "Slim Fit Jeans", "Lightweight Hoodie", "Formal Leather Belt",
                "Running Sneakers", "Men Casual Shirt", "Women Linen Blazer", "Sport Training Shorts",
                "Winter Puffer Jacket", "Canvas Backpack", "Wrist Watch Minimal", "Leather Wallet",
                "Oversized Sweatshirt", "Chino Pants", "Polo Shirt", "Travel Duffel Bag",
                "Beanie Hat", "Sunglasses Classic", "Ankle Socks Pack", "Gym Compression Top"
            ]),
        new CategorySpec(
            "Demo Beauty",
            "demo-beauty",
            "BTY",
            12000m,
            130000m,
            [
                "Vitamin C Serum", "Daily Sunscreen SPF50", "Hydrating Face Cream", "Argan Hair Oil",
                "Charcoal Face Mask", "Beard Grooming Kit", "Makeup Brush Set", "Aloe Vera Gel",
                "Keratin Shampoo", "Nail Care Kit", "Facial Cleanser", "Lip Balm Pack",
                "Retinol Night Cream", "Body Lotion", "Hair Dryer Compact", "Fragrance Mist",
                "Scalp Massager", "Eye Cream", "Hand Cream Set", "Bath Salt Jar"
            ]),
        new CategorySpec(
            "Demo Books & Stationery",
            "demo-books-stationery",
            "BKS",
            3000m,
            60000m,
            [
                "Backend Engineering Notebook", "Clean Coding Journal", "Premium Gel Pen Set", "Desk Planner 2026",
                "Sticky Notes Mega Pack", "A4 Sketchbook", "Business Card Holder", "Scientific Calculator",
                "Laptop Stickers Pack", "Document Organizer", "Hardcover Reading Journal", "Whiteboard Marker Set",
                "Mechanical Pencil Kit", "Desk Cable Clips", "Book Stand Adjustable", "Index Tabs Pack",
                "Exam Revision Cards", "Minimal Desk Calendar", "Arabic Calligraphy Notebook", "Storage File Box"
            ]),
        new CategorySpec(
            "Demo Sports",
            "demo-sports",
            "SPT",
            9000m,
            240000m,
            [
                "Adjustable Dumbbell Set", "Yoga Mat Premium", "Resistance Bands Kit", "Stainless Shaker Bottle",
                "Running Waist Bag", "Foam Roller", "Skipping Rope Pro", "Fitness Gloves",
                "Pull Up Bar", "Knee Support Brace", "Smart Body Scale", "Training Cones Set",
                "Table Tennis Racket", "Football Size 5", "Cycling Water Bottle", "Gym Towel Pack",
                "Wrist Wraps", "Ankle Weights", "Camping Flashlight", "Outdoor Folding Chair"
            ]),
        new CategorySpec(
            "Demo Gaming",
            "demo-gaming",
            "GMG",
            20000m,
            900000m,
            [
                "RGB Gaming Keyboard", "Wireless Gaming Mouse", "Gaming Headset 7.1", "Controller Charging Dock",
                "Console Cooling Stand", "Game Capture Card", "Gaming Chair", "RGB Light Strip",
                "Portable Gaming Monitor", "VR Stand Holder", "Controller Pro", "Gaming Desk Mat",
                "Streaming Microphone", "Headset Stand", "Gamepad Mobile Clip", "Mechanical Switch Tester",
                "Racing Wheel Stand", "Arcade Fight Stick", "Cable Management Kit", "Console Travel Case"
            ]),
        new CategorySpec(
            "Demo Automotive",
            "demo-automotive",
            "AUT",
            7000m,
            280000m,
            [
                "Car Vacuum Cleaner", "Tire Pressure Gauge", "Dashboard Camera", "Portable Air Compressor",
                "Car Seat Organizer", "Microfiber Towel Pack", "LED Headlight Bulb", "Car Jump Starter",
                "Windshield Sun Shade", "Trunk Storage Box", "Car Wash Kit", "Bluetooth FM Transmitter",
                "Phone Mount Vent", "Emergency Tool Kit", "Fuel Funnel", "Interior Cleaning Gel",
                "Leather Seat Cleaner", "Wheel Brush Set", "Portable Car Kettle", "Reflective Safety Vest"
            ])
    ];
}
