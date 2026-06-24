using MATGER.Api.Data;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Tests.Support;

public static class TestDataSeeder
{
    public static async Task<Category> CreateCategoryAsync(
        ApplicationDbContext dbContext,
        string slug,
        bool isActive = true)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = $"Category {slug}",
            Slug = slug,
            IsActive = isActive
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        return category;
    }

    public static async Task<Brand> CreateBrandAsync(
        ApplicationDbContext dbContext,
        string slug,
        bool isActive = true)
    {
        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = $"Brand {slug}",
            Slug = slug,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        return brand;
    }

    public static async Task<Product> CreateProductAsync(
        ApplicationDbContext dbContext,
        Category category,
        string sku,
        bool isActive = true,
        bool isFeatured = false,
        int quantityAvailable = 10,
        bool isReturnable = true,
        Brand? brand = null,
        decimal? costPrice = 10m,
        decimal? salePrice = null,
        DateTime? saleStartAtUtc = null,
        DateTime? saleEndAtUtc = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = $"Product {sku}",
            Description = $"Test product {sku}",
            SKU = sku,
            Price = 25m,
            CostPrice = costPrice,
            SalePrice = salePrice,
            SaleStartAtUtc = saleStartAtUtc,
            SaleEndAtUtc = saleEndAtUtc,
            IsActive = isActive,
            IsFeatured = isFeatured,
            CategoryId = category.Id,
            Category = category,
            BrandId = brand?.Id,
            Brand = brand,
            IsReturnable = isReturnable,
            ReturnWindowDays = 30,
            CreatedAt = DateTime.UtcNow
        };

        product.InventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            QuantityAvailable = quantityAvailable,
            QuantityReserved = 0,
            LowStockThreshold = 2,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product;
    }

    public static async Task<ProductImage> CreateProductImageAsync(
        ApplicationDbContext dbContext,
        Product product,
        bool isPrimary = true)
    {
        var image = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            ImageUrl = $"https://cdn.matger.local/products/{product.SKU.ToLowerInvariant()}.jpg",
            AltText = $"{product.Name} image",
            IsPrimary = isPrimary,
            SortOrder = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.ProductImages.Add(image);
        await dbContext.SaveChangesAsync();

        return image;
    }

    public static async Task<ProductSpecification> CreateProductSpecificationAsync(
        ApplicationDbContext dbContext,
        Product product)
    {
        var specification = new ProductSpecification
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            Name = "Material",
            Value = "Aluminum",
            GroupName = "General",
            SortOrder = 0
        };

        dbContext.ProductSpecifications.Add(specification);
        await dbContext.SaveChangesAsync();

        return specification;
    }

    public static async Task<ProductVariant> CreateVariantAsync(
        ApplicationDbContext dbContext,
        Product product,
        string sku,
        bool isActive = true,
        int quantityAvailable = 5)
    {
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            Name = $"Variant {sku}",
            SKU = sku,
            PriceOverride = 30m,
            IsActive = isActive,
            QuantityAvailable = quantityAvailable,
            QuantityReserved = 0,
            LowStockThreshold = 1,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ProductVariants.Add(variant);
        await dbContext.SaveChangesAsync();

        return variant;
    }

    public static async Task<CustomerAddress> CreateAddressAsync(
        ApplicationDbContext dbContext,
        Guid userId)
    {
        var address = new CustomerAddress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Label = "Home",
            FullName = "Customer Test User",
            PhoneNumber = "+9647000000000",
            Country = "Iraq",
            City = "Baghdad",
            Street = "Testing Street",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.CustomerAddresses.Add(address);
        await dbContext.SaveChangesAsync();

        return address;
    }

    public static async Task<ShippingMethod> CreateShippingMethodAsync(ApplicationDbContext dbContext)
    {
        var method = new ShippingMethod
        {
            Id = Guid.NewGuid(),
            Name = "Standard Shipping",
            Code = $"STD-{Guid.NewGuid():N}"[..12],
            BaseCost = 4m,
            EstimatedDeliveryDays = 3,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ShippingMethods.Add(method);
        await dbContext.SaveChangesAsync();

        return method;
    }

    public static async Task<Order> CreateOrderAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        Product product,
        OrderStatus status,
        ProductVariant? variant = null)
    {
        var now = DateTime.UtcNow;
        var unitPrice = variant?.PriceOverride ?? product.Price;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"MTG-TST-{Guid.NewGuid():N}"[..22].ToUpperInvariant(),
            UserId = userId,
            Status = status,
            Subtotal = unitPrice,
            ShippingFee = 0m,
            DiscountAmount = 0m,
            Total = unitPrice,
            ShippingStatus = status == OrderStatus.Delivered
                ? ShippingStatus.Delivered
                : status == OrderStatus.Shipped
                    ? ShippingStatus.Shipped
                    : ShippingStatus.Pending,
            CreatedAt = now,
            PaidAt = status >= OrderStatus.Paid ? now.AddMinutes(1) : null,
            ShippedAt = status == OrderStatus.Shipped || status == OrderStatus.Delivered
                ? now.AddMinutes(2)
                : null,
            DeliveredAt = status == OrderStatus.Delivered || status == OrderStatus.ReturnRequested || status == OrderStatus.Returned || status == OrderStatus.Refunded
                ? now.AddMinutes(3)
                : null
        };

        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            Order = order,
            OrderId = order.Id,
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
            Quantity = 1,
            Total = unitPrice
        });

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        return order;
    }

    public static async Task<Product> ReloadProductAsync(
        ApplicationDbContext dbContext,
        Guid productId)
    {
        return await dbContext.Products
            .Include(product => product.InventoryItem)
            .Include(product => product.Variants)
            .FirstAsync(product => product.Id == productId);
    }
}
