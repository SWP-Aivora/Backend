using Aivora.Repositories.Enums;

namespace Aivora.Services.MilestoneService;

public class Response
{
    public class MilestoneResponse
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = null!;
        public MilestoneStatus Status { get; set; }
        public DateOnly? DueDate { get; set; }
        public int OrderIndex { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? FundedAt { get; set; }
        public DateTimeOffset? DepositPaidAt { get; set; }
        public DateTimeOffset? SubmittedAt { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public DateTimeOffset? PaidAt { get; set; }
        public DateTimeOffset? ReleasedAt { get; set; }
        public List<MilestoneStepResponse>? Steps { get; set; }
    }

    public class MilestoneStepResponse
    {
        public Guid Id { get; set; }
        public Guid MilestoneId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
        public MilestoneStepStatus Status { get; set; }
        public DateOnly? DueDate { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public Guid? CompletedByUserId { get; set; }
    }

    public class FundResultResponse
    {
        public MilestoneResponse Milestone { get; set; } = null!;
        public PaymentInfo Payment { get; set; } = null!;
        public WalletInfo Wallet { get; set; } = null!;
    }

    public class PaymentInfo
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid MilestoneId { get; set; }
        public Guid PayerId { get; set; }
        public Guid PayeeId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTimeOffset? HeldAt { get; set; }
        public string BusinessMeaning => Status switch
        {
            "HELD" => "Direct transfer initiated / waiting receipt confirmation",
            "RELEASED" => "Receipt/payment completed",
            "PENDING" => "Direct transfer record created",
            _ => Status
        };
        public string Explanation => "This is a direct-transfer tracking record (legacy technical field: Payment).";
    }

    public class WalletInfo
    {
        public decimal AvailableBalance { get; set; }
        public decimal HeldBalance { get; set; }
        public string Currency { get; set; } = null!;
    }
}
