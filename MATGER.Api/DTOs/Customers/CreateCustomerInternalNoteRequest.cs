using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Customers;

public sealed class CreateCustomerInternalNoteRequest
{
    [Required]
    [MaxLength(1000)]
    public string Note { get; init; } = string.Empty;

    public bool IsImportant { get; init; }
}
