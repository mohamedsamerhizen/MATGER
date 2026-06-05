using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Orders;

public sealed class AddOrderInternalNoteRequest
{
    [Required]
    [MaxLength(2000)]
    public string Note { get; init; } = string.Empty;
}
