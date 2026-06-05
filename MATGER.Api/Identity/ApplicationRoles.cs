namespace MATGER.Api.Identity;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
    public const string OrderManager = "OrderManager";
    public const string InventoryManager = "InventoryManager";

    public static readonly string[] All = [Admin, Customer, OrderManager, InventoryManager];
}
