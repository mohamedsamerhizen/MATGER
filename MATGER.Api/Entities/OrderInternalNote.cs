using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class OrderInternalNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public Guid AuthorUserId { get; set; }

    public ApplicationUser AuthorUser { get; set; } = null!;

    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
