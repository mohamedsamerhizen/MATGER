using System.Data;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Loyalty;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminLoyaltyController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpPost("customers/{userId:guid}/loyalty/adjust")]
    public async Task<ActionResult<LoyaltyAccountResponse>> Adjust(
        Guid userId,
        AdjustLoyaltyPointsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Points == 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Loyalty adjustment points cannot be zero."));
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Loyalty adjustment note is required."));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Customer was not found."));
        }

        var now = DateTime.UtcNow;
        var account = await dbContext.LoyaltyAccounts
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (account is null)
        {
            account = new LoyaltyAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.LoyaltyAccounts.Add(account);
        }

        var nextBalance = account.PointsBalance + request.Points;

        if (nextBalance < 0)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Loyalty points cannot become negative."));
        }

        account.PointsBalance = nextBalance;
        account.UpdatedAtUtc = now;

        dbContext.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Account = account,
            Points = request.Points,
            Type = LoyaltyTransactionType.Adjusted,
            ReferenceType = "AdminLoyaltyAdjustment",
            Note = request.Note.Trim(),
            CreatedAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ToResponse(account));
    }

    [HttpGet("loyalty/summary")]
    public async Task<ActionResult<LoyaltySummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        var response = new LoyaltySummaryResponse
        {
            Accounts = await dbContext.LoyaltyAccounts.CountAsync(cancellationToken),
            PointsOutstanding = await dbContext.LoyaltyAccounts.SumAsync(account => account.PointsBalance, cancellationToken),
            LifetimeEarned = await dbContext.LoyaltyAccounts.SumAsync(account => account.LifetimeEarned, cancellationToken),
            LifetimeRedeemed = await dbContext.LoyaltyAccounts.SumAsync(account => account.LifetimeRedeemed, cancellationToken),
            Transactions = await dbContext.LoyaltyTransactions.CountAsync(cancellationToken)
        };

        return Ok(response);
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
