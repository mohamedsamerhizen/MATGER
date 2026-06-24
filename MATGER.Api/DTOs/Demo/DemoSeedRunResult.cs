namespace MATGER.Api.DTOs.Demo;

public sealed class DemoSeedRunResult
{
    public bool Enabled { get; set; }

    public bool AlreadySeeded { get; set; }

    public string Message { get; set; } = string.Empty;

    public string DemoPassword { get; set; } = string.Empty;

    public string AdminEmail { get; set; } = string.Empty;

    public string OrderManagerEmail { get; set; } = string.Empty;

    public string InventoryManagerEmail { get; set; } = string.Empty;

    public string FirstCustomerEmail { get; set; } = string.Empty;

    public int CategoriesCreated { get; set; }

    public int BrandsCreated { get; set; }

    public int ProductsCreated { get; set; }

    public int ProductImagesCreated { get; set; }

    public int ProductSpecificationsCreated { get; set; }

    public int ProductPriceHistoriesCreated { get; set; }

    public int ProductVariantsCreated { get; set; }

    public int InventoryItemsCreated { get; set; }

    public int InventoryMovementsCreated { get; set; }

    public int StockAdjustmentRequestsCreated { get; set; }

    public int CustomersCreated { get; set; }

    public int CustomerAddressesCreated { get; set; }

    public int ShippingMethodsCreated { get; set; }

    public int CouponsCreated { get; set; }

    public int CartsCreated { get; set; }

    public int CartItemsCreated { get; set; }

    public int WishlistItemsCreated { get; set; }

    public int CustomerWalletsCreated { get; set; }

    public int CustomerWalletTransactionsCreated { get; set; }

    public int LoyaltyAccountsCreated { get; set; }

    public int LoyaltyTransactionsCreated { get; set; }

    public int OrdersCreated { get; set; }

    public int OrderItemsCreated { get; set; }

    public int PaymentsCreated { get; set; }

    public int PaymentAttemptsCreated { get; set; }

    public int InventoryReservationsCreated { get; set; }

    public int CouponRedemptionsCreated { get; set; }

    public int ReturnRequestsCreated { get; set; }

    public int RefundsCreated { get; set; }

    public int ProductReviewsCreated { get; set; }

    public int OrderStatusHistoriesCreated { get; set; }

    public int OrderInternalNotesCreated { get; set; }

    public int CustomerInternalNotesCreated { get; set; }

    public int RiskSignalsCreated { get; set; }

    public int AuditLogsCreated { get; set; }
}
