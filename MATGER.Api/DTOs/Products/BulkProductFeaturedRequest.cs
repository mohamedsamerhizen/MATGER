using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class BulkProductFeaturedRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyList<Guid> ProductIds { get; init; } = [];

    public bool IsFeatured { get; init; }
}
