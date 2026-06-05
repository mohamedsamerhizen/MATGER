namespace MATGER.Api.DTOs.Products;

public sealed class BulkProductOperationResponse
{
    public int RequestedCount { get; init; }

    public int MatchedCount { get; init; }

    public int UpdatedCount { get; init; }

    public int AlreadyMatchingCount { get; init; }

    public IReadOnlyList<Guid> NotFoundProductIds { get; init; } = [];

    public IReadOnlyList<ProductResponse> Products { get; init; } = [];
}
