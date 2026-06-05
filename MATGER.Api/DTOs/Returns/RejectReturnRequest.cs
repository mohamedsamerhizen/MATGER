using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Returns;

public sealed class RejectReturnRequest
{
    [MaxLength(500)]
    public string? AdminNote { get; init; }
}
