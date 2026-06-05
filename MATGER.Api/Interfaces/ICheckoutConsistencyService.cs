using MATGER.Api.DTOs.CheckoutConsistency;
using MATGER.Api.DTOs.Common;

namespace MATGER.Api.Interfaces;

public interface ICheckoutConsistencyService
{
    Task<CheckoutConsistencySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<CheckoutConsistencyIssueResponse>> GetIssuesAsync(
        string? severity,
        string? issueType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<CheckoutMaintenanceRunResponse> ExpirePendingPaymentsAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
