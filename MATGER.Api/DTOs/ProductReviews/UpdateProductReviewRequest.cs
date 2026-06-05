using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.ProductReviews;

public sealed class UpdateProductReviewRequest
{
    [Range(1, 5)]
    public int? Rating { get; init; }

    [MaxLength(1000)]
    public string? Comment { get; init; }
}
