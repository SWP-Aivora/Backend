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

    public void Debit(decimal amount, bool bypassDebtLimit = false)
    {
        if (amount < 0) throw new ArgumentException("Amount must be non-negative.", nameof(amount));

        if (AvailableBalance >= amount)
        {
            AvailableBalance -= amount;
        }
        else
        {
            var deficit = amount - AvailableBalance;
            decimal limit = bypassDebtLimit ? 5000m : 1000m;
            if (Debt + deficit > limit)
            {
                throw new InvalidOperationException($"Clawback failed. Operation would exceed the maximum {(bypassDebtLimit ? "system " : "")}debt limit of {limit} {Currency}.");
            }
            AvailableBalance = 0;
            Debt += deficit;
        }
    }
}

