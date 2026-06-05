namespace MATGER.Api.Identity;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";

    public const string InventoryManagerOnly = "InventoryManagerOnly";

    public const string OrderManagerOnly = "OrderManagerOnly";

    public const string CustomerOnly = "CustomerOnly";
}