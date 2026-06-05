using System.Data;
using System.Security.Cryptography;
using System.Text;
using MATGER.Api.Data;
using MATGER.Api.Entities;
using MATGER.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class RefreshTokenService(ApplicationDbContext dbContext) : IRefreshTokenService
{
    private const int RefreshTokenExpirationDays = 7;

    public async Task<string> CreateRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateSecureToken();
        var tokenHash = HashToken(rawToken);
        var now = DateTime.UtcNow;

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(RefreshTokenExpirationDays),
            IsUsed = false
        };

        dbContext.RefreshTokens.Add(refreshToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    public async Task<(bool IsValid, Guid UserId, string NewRefreshToken)> RotateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return (false, Guid.Empty, string.Empty);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var tokenHash = HashToken(refreshToken.Trim());

        var existingRefreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (existingRefreshToken is null)
        {
            await transaction.CommitAsync(cancellationToken);

            return (false, Guid.Empty, string.Empty);
        }

        if (!existingRefreshToken.IsActive)
        {
            await transaction.CommitAsync(cancellationToken);

            return (false, Guid.Empty, string.Empty);
        }

        var newRawToken = GenerateSecureToken();
        var newTokenHash = HashToken(newRawToken);
        var now = DateTime.UtcNow;

        existingRefreshToken.IsUsed = true;
        existingRefreshToken.RevokedAtUtc = now;
        existingRefreshToken.ReplacedByTokenHash = newTokenHash;

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingRefreshToken.UserId,
            TokenHash = newTokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(RefreshTokenExpirationDays),
            IsUsed = false
        };

        dbContext.RefreshTokens.Add(newRefreshToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (true, existingRefreshToken.UserId, newRawToken);
    }

    public async Task<bool> RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var tokenHash = HashToken(refreshToken.Trim());

        var existingRefreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (existingRefreshToken is null)
        {
            await transaction.CommitAsync(cancellationToken);

            return false;
        }

        if (!existingRefreshToken.IsActive)
        {
            await transaction.CommitAsync(cancellationToken);

            return false;
        }

        existingRefreshToken.RevokedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(tokenBytes);

        return Convert.ToBase64String(hashBytes);
    }
}