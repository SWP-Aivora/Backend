namespace Aivora.Services.IdentityService;

public class Request
{
    public class LoginRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Role { get; set; } = null!; // "CLIENT" or "EXPERT"
        public string FullName { get; set; } = null!;
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = null!;
    }
}
