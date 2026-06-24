using MATGER.Api.Entities;

namespace MATGER.Api.Interfaces;

public interface IRiskSignalService
{
    Task EvaluateOrderAsync(
        Order order,
        CustomerAddress shippingAddress,
        CancellationToken cancellationToken = default);
}
