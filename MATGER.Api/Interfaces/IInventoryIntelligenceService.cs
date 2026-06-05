using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Inventory;

namespace MATGER.Api.Interfaces;

public interface IInventoryIntelligenceService
{
    Task<InventoryHealthSummaryResponse> GetHealthSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<InventoryAttentionItemResponse>> GetNeedsAttentionAsync(
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<TopReservedProductResponse>> GetTopReservedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
