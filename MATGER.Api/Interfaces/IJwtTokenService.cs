namespace MATGER.Api.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string> roles);
}