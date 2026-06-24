using System.Data;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Common;
using MATGER.Api.DTOs.Wallet;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/admin/customers/{userId:guid}/wallet")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminCustomerWalletsController(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpPost("credit")]
    public async Task<ActionResult<CustomerWalletResponse>> Credit(
        Guid userId,
        WalletAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        return await AdjustAsync(
            userId,
            request,
            CustomerWalletTransactionType.Credit,
            cancellationToken);
    }

    [HttpPost("debit")]
    public async Task<ActionResult<CustomerWalletResponse>> Debit(
        Guid userId,
        WalletAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        return await AdjustAsync(
            userId,
            request,
            CustomerWalletTransactionType.Debit,
            cancellationToken);
    }

    private async Task<ActionResult<CustomerWalletResponse>> AdjustAsync(
        Guid userId,
        WalletAdjustmentRequest request,
        CustomerWalletTransactionType type,
        CancellationToken cancellationToken)
    {
        var actorUserId = currentUserService.UserId;

        if (actorUserId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        if (request.Amount <= 0m)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Wallet adjustment amount must be positive."));
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Wallet adjustment note is required."));
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

        var wallet = await dbContext.CustomerWallets
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var now = DateTime.UtcNow;

        if (wallet is null)
        {
            wallet = new CustomerWallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Balance = 0m,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.CustomerWallets.Add(wallet);
        }

        if (type == CustomerWalletTransactionType.Debit &&
            wallet.Balance < request.Amount)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Wallet balance cannot become negative."));
        }

        wallet.Balance = type == CustomerWalletTransactionType.Credit
            ? wallet.Balance + request.Amount
            : wallet.Balance - request.Amount;
        wallet.UpdatedAtUtc = now;

        dbContext.CustomerWalletTransactions.Add(new CustomerWalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Wallet = wallet,
            Amount = request.Amount,
            Type = type,
            ReferenceType = "AdminWalletAdjustment",
            Note = request.Note.Trim(),
            CreatedAtUtc = now,
            CreatedByUserId = actorUserId.Value
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ToResponse(wallet));
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
