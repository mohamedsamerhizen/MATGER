namespace MATGER.Api.DTOs.Checkout;

public sealed class CheckoutStartRequest
{
    public Guid? ShippingAddressId { get; init; }

    public Guid? ShippingMethodId { get; init; }
}
