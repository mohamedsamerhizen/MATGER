using MATGER.Api.Enums;

namespace MATGER.Api.Entities;

public sealed class PaymentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public Payment Payment { get; set; } = null!;

    public int AttemptNumber { get; set; }

    public PaymentAttemptStatus Status { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}