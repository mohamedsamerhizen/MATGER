namespace MATGER.Api.Interfaces;

public interface IRefreshTokenService
{
    Task<string> CreateRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(bool IsValid, Guid UserId, string NewRefreshToken)> RotateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}