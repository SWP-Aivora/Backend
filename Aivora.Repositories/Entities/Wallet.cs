using Aivora.Repositories.Abstractions;
namespace Aivora.Repositories.Entities;

public class Wallet : AuditableBaseEntity
{
    public Guid UserId { get; set; }
    public decimal AvailableBalance { get; set; } = 0;
    public decimal HeldBalance { get; set; } = 0;
    public decimal TotalEarned { get; set; } = 0;
    public decimal Debt { get; set; } = 0;
    public string Currency { get; set; } = "AICOIN";

    // Navigation Properties
    public virtual User User { get; set; } = null!;

    public void Credit(decimal amount)
    {
        if (amount < 0) throw new ArgumentException("Amount must be non-negative.", nameof(amount));

        if (Debt > 0)
        {
            var debtPayment = Math.Min(amount, Debt);
            Debt -= debtPayment;
            amount -= debtPayment;
        }
        AvailableBalance += amount;
    }

    public bool CanDebit(decimal amount, out string? reason)
    {
        return CanDebit(amount, 1000m, out reason);
    }

    public bool CanDebit(decimal amount, decimal maxDebtLimit, out string? reason)
    {
        reason = null;
        if (amount < 0)
        {
            reason = "Amount must be non-negative.";
            return false;
        }

        var available = Math.Max(0, AvailableBalance);
        if (available >= amount)
        {
            return true;
        }

        var deficit = amount - available;
        if (maxDebtLimit > 0 && Debt + deficit > maxDebtLimit)
        {
            reason = $"Clawback failed. Operation would exceed the maximum debt limit of {maxDebtLimit} {Currency}. Current debt: {Debt}, deficit: {deficit}.";
            return false;
        }

        return true;
    }

    public void Debit(decimal amount, decimal maxDebtLimit = 1000m)
    {
        if (!CanDebit(amount, maxDebtLimit, out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        var available = Math.Max(0, AvailableBalance);
        if (available >= amount)
        {
            AvailableBalance = available - amount;
        }
        else
        {
            var deficit = amount - available;
            AvailableBalance = 0;
            Debt += deficit;
        }
    }
}

