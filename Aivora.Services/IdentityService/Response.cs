namespace Aivora.Services.IdentityService;

public class Response
{
    public class IdentityResponse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public Guid UserId { get; set; }
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;
    }

    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public string Role { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTimeOffset? LastLoginAt { get; set; }
    }
}
