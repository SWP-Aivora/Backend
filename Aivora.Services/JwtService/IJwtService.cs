using Aivora.Repositories.Entities;

namespace Aivora.Services.JwtService;

public interface IJwtService
{
    string GenerateAccessToken(User user, string role);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
