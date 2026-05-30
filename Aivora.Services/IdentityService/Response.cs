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
}
