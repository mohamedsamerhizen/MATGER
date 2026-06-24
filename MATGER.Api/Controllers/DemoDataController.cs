using MATGER.Api.Data;
using MATGER.Api.DTOs.Demo;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/demo-data")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class DemoDataController(
    ApplicationDbContext dbContext,
    DemoDataSeeder demoDataSeeder,
    IOptions<DemoSeedOptions> demoSeedOptions) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DemoDataSummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        var options = demoSeedOptions.Value;

        var response = new DemoDataSummaryResponse
        {
            DemoSeedEnabled = options.Enabled,
            DemoPassword = options.DemoPassword,
            AdminEmail = "admin@matger.local",
            OrderManagerEmail = "order.manager@matger.local",
            InventoryManagerEmail = "inventory.manager@matger.local",
            FirstCustomerEmail = "customer01@matger.local",
            Users = await dbContext.Users.CountAsync(cancellationToken),
            Categories = await dbContext.Categories.CountAsync(cancellationToken),
            Brands = await dbContext.Brands.CountAsync(cancellationToken),
            Products = await dbContext.Products.CountAsync(cancellationToken),
            ProductImages = await dbContext.ProductImages.CountAsync(cancellationToken),
            ProductSpecifications = await dbContext.ProductSpecifications.CountAsync(cancellationToken),
            ProductPriceHistories = await dbContext.ProductPriceHistories.CountAsync(cancellationToken),
            ProductVariants = await dbContext.ProductVariants.CountAsync(cancellationToken),
            InventoryItems = await dbContext.InventoryItems.CountAsync(cancellationToken),
            InventoryMovements = await dbContext.InventoryMovements.CountAsync(cancellationToken),
            StockAdjustmentRequests = await dbContext.StockAdjustmentRequests.CountAsync(cancellationToken),
            ShippingMethods = await dbContext.ShippingMethods.CountAsync(cancellationToken),
            Coupons = await dbContext.Coupons.CountAsync(cancellationToken),
            Carts = await dbContext.Carts.CountAsync(cancellationToken),
            CartItems = await dbContext.CartItems.CountAsync(cancellationToken),
            WishlistItems = await dbContext.WishlistItems.CountAsync(cancellationToken),
            CustomerWallets = await dbContext.CustomerWallets.CountAsync(cancellationToken),
            CustomerWalletTransactions = await dbContext.CustomerWalletTransactions.CountAsync(cancellationToken),
            LoyaltyAccounts = await dbContext.LoyaltyAccounts.CountAsync(cancellationToken),
            LoyaltyTransactions = await dbContext.LoyaltyTransactions.CountAsync(cancellationToken),
            Orders = await dbContext.Orders.CountAsync(cancellationToken),
            OrderItems = await dbContext.OrderItems.CountAsync(cancellationToken),
            Payments = await dbContext.Payments.CountAsync(cancellationToken),
            PaymentAttempts = await dbContext.PaymentAttempts.CountAsync(cancellationToken),
            InventoryReservations = await dbContext.InventoryReservations.CountAsync(cancellationToken),
            CouponRedemptions = await dbContext.CouponRedemptions.CountAsync(cancellationToken),
            ReturnRequests = await dbContext.ReturnRequests.CountAsync(cancellationToken),
            Refunds = await dbContext.Refunds.CountAsync(cancellationToken),
            ProductReviews = await dbContext.ProductReviews.CountAsync(cancellationToken),
            OrderStatusHistories = await dbContext.OrderStatusHistories.CountAsync(cancellationToken),
            OrderInternalNotes = await dbContext.OrderInternalNotes.CountAsync(cancellationToken),
            CustomerInternalNotes = await dbContext.CustomerInternalNotes.CountAsync(cancellationToken),
            RiskSignals = await dbContext.RiskSignals.CountAsync(cancellationToken),
            AuditLogs = await dbContext.AuditLogs.CountAsync(cancellationToken)
        };

        return Ok(response);
    }

    [HttpPost("seed")]
    public async Task<ActionResult<DemoSeedRunResult>> Seed(
        CancellationToken cancellationToken)
    {
        var response = await demoDataSeeder.SeedAsync(
            cancellationToken,
            force: true);

        return Ok(response);
    }
}
