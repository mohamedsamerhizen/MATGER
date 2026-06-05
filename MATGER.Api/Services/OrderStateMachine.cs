using MATGER.Api.Enums;

namespace MATGER.Api.Services;

public static class OrderStateMachine
{
    private static readonly Dictionary<OrderStatus, IReadOnlySet<OrderStatus>> AllowedTransitions = new()
    {
        [OrderStatus.Draft] = new HashSet<OrderStatus>
        {
            OrderStatus.PendingPayment
        },

        [OrderStatus.PendingPayment] = new HashSet<OrderStatus>
        {
            OrderStatus.Paid,
            OrderStatus.PaymentFailed,
            OrderStatus.Cancelled
        },

        [OrderStatus.PaymentFailed] = new HashSet<OrderStatus>
        {
            OrderStatus.PendingPayment
        },

        [OrderStatus.Paid] = new HashSet<OrderStatus>
        {
            OrderStatus.Processing
        },

        [OrderStatus.Processing] = new HashSet<OrderStatus>
        {
            OrderStatus.Shipped
        },

        [OrderStatus.Shipped] = new HashSet<OrderStatus>
        {
            OrderStatus.Delivered
        },

        [OrderStatus.Delivered] = new HashSet<OrderStatus>
        {
            OrderStatus.ReturnRequested
        },

        [OrderStatus.ReturnRequested] = new HashSet<OrderStatus>
        {
            OrderStatus.Returned
        },

        [OrderStatus.Returned] = new HashSet<OrderStatus>
        {
            OrderStatus.Refunded
        },

        [OrderStatus.Cancelled] = new HashSet<OrderStatus>(),

        [OrderStatus.Refunded] = new HashSet<OrderStatus>()
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var allowedStatuses) &&
               allowedStatuses.Contains(to);
    }
}