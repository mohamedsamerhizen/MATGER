using System.Data;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Wallet;
using MATGER.Api.Entities;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
public sealed class WalletController(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CustomerWalletResponse>> GetWallet(
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var wallet = await EnsureWalletAsync(userId.Value, cancellationToken);

        return Ok(ToResponse(wallet));
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<CustomerWalletTransactionResponse>>> GetTransactions(
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var wallet = await EnsureWalletAsync(userId.Value, cancellationToken);
        var transactions = await dbContext.CustomerWalletTransactions
            .AsNoTracking()
            .Where(transaction => transaction.WalletId == wallet.Id)
            .OrderByDescending(transaction => transaction.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(transactions.Select(ToResponse).ToList());
    }

    private async Task<CustomerWallet> EnsureWalletAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var wallet = await dbContext.CustomerWallets
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (wallet is null)
        {
            wallet = new CustomerWallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Balance = 0m,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.CustomerWallets.Add(wallet);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return wallet;
    }

    private static CustomerWalletResponse ToResponse(CustomerWallet wallet)
    {
        return new CustomerWalletResponse
        {
            WalletId = wallet.Id,
            UserId = wallet.UserId,
            Balance = wallet.Balance,
            CreatedAtUtc = wallet.CreatedAtUtc,
            UpdatedAtUtc = wallet.UpdatedAtUtc
        };
    }

    private static CustomerWalletTransactionResponse ToResponse(CustomerWalletTransaction transaction)
    {
        return new CustomerWalletTransactionResponse
        {
            Id = transaction.Id,
            WalletId = transaction.WalletId,
            Amount = transaction.Amount,
            Type = transaction.Type.ToString(),
            ReferenceType = transaction.ReferenceType,
            ReferenceId = transaction.ReferenceId,
            Note = transaction.Note,
            CreatedAtUtc = transaction.CreatedAtUtc,
            CreatedByUserId = transaction.CreatedByUserId
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
