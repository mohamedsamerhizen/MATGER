using System.Data;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Loyalty;
using MATGER.Api.Entities;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/loyalty")]
[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
public sealed class LoyaltyController(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LoyaltyAccountResponse>> GetAccount(
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var account = await EnsureAccountAsync(userId.Value, cancellationToken);

        return Ok(ToResponse(account));
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyTransactionResponse>>> GetTransactions(
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var account = await EnsureAccountAsync(userId.Value, cancellationToken);
        var transactions = await dbContext.LoyaltyTransactions
            .AsNoTracking()
            .Where(transaction => transaction.AccountId == account.Id)
            .OrderByDescending(transaction => transaction.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(transactions.Select(ToResponse).ToList());
    }

    private async Task<LoyaltyAccount> EnsureAccountAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var account = await dbContext.LoyaltyAccounts
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (account is null)
        {
            account = new LoyaltyAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.LoyaltyAccounts.Add(account);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return account;
    }

    private static LoyaltyAccountResponse ToResponse(LoyaltyAccount account)
    {
        return new LoyaltyAccountResponse
        {
            AccountId = account.Id,
            UserId = account.UserId,
            PointsBalance = account.PointsBalance,
            LifetimeEarned = account.LifetimeEarned,
            LifetimeRedeemed = account.LifetimeRedeemed,
            CreatedAtUtc = account.CreatedAtUtc,
            UpdatedAtUtc = account.UpdatedAtUtc
        };
    }

    private static LoyaltyTransactionResponse ToResponse(LoyaltyTransaction transaction)
    {
        return new LoyaltyTransactionResponse
        {
            Id = transaction.Id,
            AccountId = transaction.AccountId,
            Points = transaction.Points,
            Type = transaction.Type.ToString(),
            ReferenceType = transaction.ReferenceType,
            ReferenceId = transaction.ReferenceId,
            Note = transaction.Note,
            CreatedAtUtc = transaction.CreatedAtUtc
        };
    }

    private ApiErrorResponse Error(int statusCode, string message)
    {
        return new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        };
    }
}
