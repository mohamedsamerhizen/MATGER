using MATGER.Api.DTOs.Coupons;

namespace MATGER.Api.Interfaces;

public interface ICouponService
{
    string NormalizeCode(string code);

    Task<ValidateCouponResponse> ValidateAsync(
        string code,
        decimal subtotal,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
