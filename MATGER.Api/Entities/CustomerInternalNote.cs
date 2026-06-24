using MATGER.Api.Identity;

namespace MATGER.Api.Entities;

public sealed class CustomerInternalNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public ApplicationUser Customer { get; set; } = null!;

    public string Note { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsImportant { get; set; }
}
