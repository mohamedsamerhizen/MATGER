using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.ProductReviews;

public sealed class ModerateProductReviewRequest
{
    [MaxLength(500)]
    public string? AdminNote { get; init; }
}
