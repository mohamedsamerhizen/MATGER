namespace MATGER.Api.Enums;

public enum OrderStatus
{
    Draft = 1,
    PendingPayment = 2,
    PaymentFailed = 3,
    Paid = 4,
    Processing = 5,
    Shipped = 6,
    Delivered = 7,
    Cancelled = 8,
    ReturnRequested = 9,
    Returned = 10,
    Refunded = 11
}