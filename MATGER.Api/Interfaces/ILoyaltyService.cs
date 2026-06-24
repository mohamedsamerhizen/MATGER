using MATGER.Api.Entities;

namespace MATGER.Api.Interfaces;

public interface ILoyaltyService
{
    Task AwardForDeliveredOrderAsync(
        Order order,
        CancellationToken cancellationToken = default);
}
