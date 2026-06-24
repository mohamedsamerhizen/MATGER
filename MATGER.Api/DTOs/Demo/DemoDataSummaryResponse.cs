namespace MATGER.Api.DTOs.Demo;

public sealed class DemoDataSummaryResponse
{
    public bool DemoSeedEnabled { get; set; }

    public string DemoPassword { get; set; } = string.Empty;

    public string AdminEmail { get; set; } = string.Empty;

    public string OrderManagerEmail { get; set; } = string.Empty;

    public string InventoryManagerEmail { get; set; } = string.Empty;

    public string FirstCustomerEmail { get; set; } = string.Empty;

    public int Users { get; set; }

    public int Categories { get; set; }

    public int Brands { get; set; }

    public int Products { get; set; }

    public int ProductImages { get; set; }

    public int ProductSpecifications { get; set; }

    public int ProductPriceHistories { get; set; }

    public int ProductVariants { get; set; }

    public int InventoryItems { get; set; }

    public int InventoryMovements { get; set; }

    public int StockAdjustmentRequests { get; set; }

    public int ShippingMethods { get; set; }

    public int Coupons { get; set; }

    public int Carts { get; set; }

    public int CartItems { get; set; }

    public int WishlistItems { get; set; }

    public int CustomerWallets { get; set; }

    public int CustomerWalletTransactions { get; set; }

    public int LoyaltyAccounts { get; set; }

    public int LoyaltyTransactions { get; set; }

    public int Orders { get; set; }

    public int OrderItems { get; set; }

    public int Payments { get; set; }

    public int PaymentAttempts { get; set; }

    public int InventoryReservations { get; set; }

    public int CouponRedemptions { get; set; }

    public int ReturnRequests { get; set; }

    public int Refunds { get; set; }

    public int ProductReviews { get; set; }

    public int OrderStatusHistories { get; set; }

    public int OrderInternalNotes { get; set; }

    public int CustomerInternalNotes { get; set; }

    public int RiskSignals { get; set; }

    public int AuditLogs { get; set; }
}
