namespace MATGER.Api.DTOs.Customers;

public sealed class CustomerInternalNoteResponse
{
    public Guid Id { get; init; }

    public Guid CustomerId { get; init; }

    public string Note { get; init; } = string.Empty;

    public Guid CreatedByUserId { get; init; }

    public string CreatedByUserName { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public bool IsImportant { get; init; }
}
