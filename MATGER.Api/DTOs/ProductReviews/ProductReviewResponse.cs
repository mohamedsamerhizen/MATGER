namespace MATGER.Api.DTOs.ProductReviews;

public sealed class ProductReviewResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public Guid UserId { get; init; }

    public string UserFullName { get; init; } = string.Empty;

    public Guid OrderId { get; init; }

    public int Rating { get; init; }

    public string? Comment { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public DateTime? HiddenAt { get; init; }

    public string? AdminNote { get; init; }
}
