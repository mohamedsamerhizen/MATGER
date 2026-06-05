using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Products;

public sealed class BulkProductStatusRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyList<Guid> ProductIds { get; init; } = [];

    public bool IsActive { get; init; }
}
