using Aivora.Repositories.Abstractions;
namespace Aivora.Repositories.Entities;

public class ClientProfile : AuditableBaseEntity
{
    public Guid UserId { get; set; }
    public string? CompanyName { get; set; }
    public string? Industry { get; set; }
    public string? CompanySize { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public decimal Rating { get; set; }
    public int TotalReviews { get; set; }

    // Navigation Properties
    public virtual User User { get; set; } = null!;
}

