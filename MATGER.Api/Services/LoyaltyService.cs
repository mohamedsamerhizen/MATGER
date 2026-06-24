using MATGER.Api.Data;
using MATGER.Api.Entities;
using MATGER.Api.Enums;
using MATGER.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Services;

public sealed class LoyaltyService(
    ApplicationDbContext dbContext,
    IConfiguration configuration) : ILoyaltyService
{
    public async Task AwardForDeliveredOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        if (order.Status != OrderStatus.Delivered || order.Total <= 0m)
        {
            return;
        }

        var referenceId = order.Id.ToString();
        var alreadyAwarded = await dbContext.LoyaltyTransactions
            .AnyAsync(transaction =>
                transaction.Type == LoyaltyTransactionType.Earned &&
                transaction.ReferenceType == nameof(Order) &&
                transaction.ReferenceId == referenceId,
                cancellationToken);

        if (alreadyAwarded)
        {
            return;
        }

        var currencyUnitsPerPoint = configuration.GetValue("Loyalty:CurrencyUnitsPerPoint", 1000m);

        if (currencyUnitsPerPoint <= 0m)
        {
            currencyUnitsPerPoint = 1000m;
        }

        var points = (int)Math.Floor(order.Total / currencyUnitsPerPoint);

        if (points <= 0)
        {
            points = 1;
        }

        var now = DateTime.UtcNow;
        var account = await dbContext.LoyaltyAccounts
            .FirstOrDefaultAsync(item => item.UserId == order.UserId, cancellationToken);

        if (account is null)
        {
            account = new LoyaltyAccount
            {
                Id = Guid.NewGuid(),
                UserId = order.UserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.LoyaltyAccounts.Add(account);
        }

        account.PointsBalance += points;
        account.LifetimeEarned += points;
        account.UpdatedAtUtc = now;

        dbContext.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Account = account,
            Points = points,
            Type = LoyaltyTransactionType.Earned,
            ReferenceType = nameof(Order),
            ReferenceId = referenceId,
            Note = $"Points earned for delivered order {order.OrderNumber}.",
            CreatedAtUtc = now
        });
    }
}
