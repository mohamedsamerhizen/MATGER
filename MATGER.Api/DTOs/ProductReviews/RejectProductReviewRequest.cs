using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.ProductReviews;

public sealed class RejectProductReviewRequest
{
    [MaxLength(500)]
    public string? AdminNote { get; init; }
}
