using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Auth;

public sealed class LogoutRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}