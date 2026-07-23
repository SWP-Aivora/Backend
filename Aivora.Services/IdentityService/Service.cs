using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.JwtService;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.IdentityService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public Service(AivoraDbContext dbContext, IJwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Response.IdentityResponse> LoginAsync(Request.LoginRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (user.Status == UserStatus.SUSPENDED)
        {
            throw new UnauthorizedException("Your account has been suspended. Please contact support.");
        }

        var accessToken = _jwtService.GenerateAccessToken(user, user.Role.ToString());
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = _jwtService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7);
        await _dbContext.SaveChangesAsync();

        return new Response.IdentityResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString()
        };
    }

    public async Task<Response.IdentityResponse> RegisterAsync(Request.RegisterRequest request)
    {
        var roleString = request.Role.ToUpper();
        if (!Enum.TryParse<UserRole>(roleString, out var role) || (role != UserRole.CLIENT && role != UserRole.EXPERT))
        {
            throw new ValidationException("Role must be either CLIENT or EXPERT.");
        }

        var existingUser = await _dbContext.Users.AnyAsync(u => u.Email == request.Email);
        if (existingUser)
        {
            throw new ValidationException("Email is already in use.");
        }

        if (request.FullName != null && request.FullName.Length > 255)
        {
            throw new ValidationException("Full name must not exceed 255 characters.");
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            FullName = request.FullName,
            Status = UserStatus.ACTIVE,
            Wallet = new Wallet { AvailableBalance = 0, Currency = "AICOIN" }
        };

        if (role == UserRole.CLIENT)
        {
            user.ClientProfile = new ClientProfile
            {
                // Add any required fields if needed later
            };
        }
        else
        {
            user.ExpertProfile = new ExpertProfile
            {
                Title = "New Expert"
                // Add any required fields if needed later
            };
        }

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var accessToken = _jwtService.GenerateAccessToken(user, user.Role.ToString());
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = _jwtService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7);
        await _dbContext.SaveChangesAsync();

        return new Response.IdentityResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString()
        };
    }

    public async Task<Response.IdentityResponse> RefreshTokenAsync(Request.RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        var refreshTokenHash = _jwtService.HashRefreshToken(request.RefreshToken);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshTokenHash);
        if (user == null || user.RefreshTokenExpiryTime <= DateTimeOffset.UtcNow)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        if (user.Status == UserStatus.SUSPENDED)
        {
            throw new UnauthorizedException("Your account has been suspended. Please contact support.");
        }

        var accessToken = _jwtService.GenerateAccessToken(user, user.Role.ToString());
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = _jwtService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7);
        await _dbContext.SaveChangesAsync();

        return new Response.IdentityResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString()
        };
    }

    public async Task<Response.UserResponse> GetCurrentUserAsync(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User not found.");

        return new Response.UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl,
            Phone = user.Phone,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            LastLoginAt = user.LastLoginAt
        };
    }
}
