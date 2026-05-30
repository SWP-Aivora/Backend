using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; } = UserStatus.ACTIVE;
    public DateTimeOffset? LastLoginAt { get; set; }

    // Navigation Properties
    public virtual ClientProfile? ClientProfile { get; set; }
    public virtual ExpertProfile? ExpertProfile { get; set; }
    public virtual Wallet? Wallet { get; set; }
}
