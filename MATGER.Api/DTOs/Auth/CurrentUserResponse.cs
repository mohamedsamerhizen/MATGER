namespace MATGER.Api.DTOs.Auth;

public sealed class CurrentUserResponse
{
    public string UserId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = [];
}