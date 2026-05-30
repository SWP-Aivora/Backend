using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class Payment : AuditableBaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid MilestoneId { get; set; }
    public Guid PayerId { get; set; }
    public Guid PayeeId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "AICOIN";
    public PaymentStatus Status { get; set; } = PaymentStatus.PENDING;
    public string? Description { get; set; }
    public DateTimeOffset? HeldAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
    public DateTimeOffset? FrozenAt { get; set; }

    // Navigation Properties
    public virtual Project Project { get; set; } = null!;
    public virtual Milestone Milestone { get; set; } = null!;
    public virtual User Payer { get; set; } = null!;
    public virtual User Payee { get; set; } = null!;
}

