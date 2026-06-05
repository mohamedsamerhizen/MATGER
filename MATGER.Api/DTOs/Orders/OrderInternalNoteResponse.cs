namespace MATGER.Api.DTOs.Orders;

public sealed class OrderInternalNoteResponse
{
    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public Guid AuthorUserId { get; init; }

    public string? AuthorFullName { get; init; }

    public string? AuthorEmail { get; init; }

    public string Note { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}
