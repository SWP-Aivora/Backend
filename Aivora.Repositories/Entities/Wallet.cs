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
        reason = null;
        if (amount < 0)
        {
            reason = "Amount must be non-negative.";
            return false;
        }

        if (AvailableBalance >= amount)
        {
            return true;
        }

        var deficit = amount - AvailableBalance;
        if (Debt + deficit > 1000m)
        {
            reason = $"Clawback failed. Operation would exceed the maximum debt limit of 1000 {Currency}. Current debt: {Debt}, deficit: {deficit}.";
            return false;
        }

        return true;
    }

    public void Debit(decimal amount)
    {
        if (!CanDebit(amount, out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        if (AvailableBalance >= amount)
        {
            AvailableBalance -= amount;
        }
        else
        {
            var deficit = amount - AvailableBalance;
            AvailableBalance = 0;
            Debt += deficit;
        }
    }
}

