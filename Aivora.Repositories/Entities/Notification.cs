using Aivora.Repositories.Abstractions;

namespace Aivora.Repositories.Entities;

public class Notification : AuditableBaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; } = false;
    public string? Type { get; set; } // PROPOSAL, MILESTONE, PAYMENT, DISPUTE, etc.

    // Navigation Properties
    public virtual User User { get; set; } = null!;
}
