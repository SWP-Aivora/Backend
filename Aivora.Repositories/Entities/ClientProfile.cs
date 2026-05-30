namespace Aivora.Repositories.Entities;

public class ClientProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string? CompanyName { get; set; }
    public string? Industry { get; set; }
    public string? CompanySize { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }

    // Navigation Properties
    public virtual User User { get; set; } = null!;
}
