namespace Aivora.Repositories.Entities;

public class Wallet : BaseEntity
{
    public Guid UserId { get; set; }
    public decimal AvailableBalance { get; set; } = 0;
    public decimal HeldBalance { get; set; } = 0;
    public decimal TotalEarned { get; set; } = 0;
    public string Currency { get; set; } = "AICOIN";

    // Navigation Properties
    public virtual User User { get; set; } = null!;
}
