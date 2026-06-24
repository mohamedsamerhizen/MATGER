using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Risk;

public sealed class ReviewRiskSignalRequest
{
    [MaxLength(500)]
    public string? ResolutionNote { get; init; }
}
