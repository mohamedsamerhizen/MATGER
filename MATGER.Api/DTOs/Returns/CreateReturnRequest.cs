using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Returns;

public sealed class CreateReturnRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
