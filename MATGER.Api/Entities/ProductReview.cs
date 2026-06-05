using MATGER.Api.Enums;
using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class ProductReview
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public ProductReviewStatus Status { get; set; } = ProductReviewStatus.Visible;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? HiddenAt { get; set; }

    public Guid? HiddenByUserId { get; set; }

    public ApplicationUser? HiddenByUser { get; set; }

    public string? AdminNote { get; set; }
}
