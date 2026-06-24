using MATGER.Api.Entities;

namespace MATGER.Api.Helpers;

public static class ProductPricingHelper
{
    public static bool IsSaleActive(Product product, DateTime nowUtc)
    {
        return product.SalePrice.HasValue &&
               product.SaleStartAtUtc.HasValue &&
               product.SaleEndAtUtc.HasValue &&
               product.SaleStartAtUtc.Value <= nowUtc &&
               product.SaleEndAtUtc.Value > nowUtc;
    }

    public static decimal GetEffectivePrice(Product product, DateTime nowUtc)
    {
        return IsSaleActive(product, nowUtc)
            ? product.SalePrice!.Value
            : product.Price;
    }
}
