using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.IdentityService;
using Aivora.Services.JwtService;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class IdentityServiceTests
{
    private readonly Mock<IJwtService> _jwtServiceMock;

    public IdentityServiceTests()
    {
        _jwtServiceMock = new Mock<IJwtService>();
        _jwtServiceMock.Setup(x => x.HashRefreshToken(It.IsAny<string>()))
            .Returns<string>(token => $"hash:{token}");
    }

    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    [Fact]
    public async Task RegisterAsync_CreatesUser_Wallet_AndProfile()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.IdentityService.Service(dbContext, _jwtServiceMock.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.JwtOptions()));
        var request = new Request.RegisterRequest { Email = "e@t.com", FullName = "Name", Password = "p", Role = "EXPERT" };

        _jwtServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<string>())).Returns("at");
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("rt");

        // Act
        await service.RegisterAsync(request);

        // Assert
        var user = await dbContext.Users.Include(u => u.ExpertProfile).Include(u => u.Wallet).FirstOrDefaultAsync(u => u.Email == request.Email);
        user.Should().NotBeNull();
        user!.ExpertProfile.Should().NotBeNull();
        user.Wallet.Should().NotBeNull();
        user.Wallet!.AvailableBalance.Should().Be(0);
        user.RefreshToken.Should().Be("hash:rt");
    }

    [Fact]
    public async Task RegisterAsync_ThrowsValidationException_WhenFullNameExceeds255Chars()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.IdentityService.Service(dbContext, _jwtServiceMock.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.JwtOptions()));
        var request = new Request.RegisterRequest { Email = "e@t.com", FullName = new string('a', 256), Password = "p", Role = "EXPERT" };

        // Act
        Func<Task> act = async () => await service.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RegisterAsync_Succeeds_WhenFullNameIsExactly255Chars()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.IdentityService.Service(dbContext, _jwtServiceMock.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.JwtOptions()));
        var request = new Request.RegisterRequest { Email = "e@t.com", FullName = new string('a', 255), Password = "p", Role = "EXPERT" };

        _jwtServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<string>())).Returns("at");
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("rt");

        // Act
        await service.RegisterAsync(request);

        // Assert
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        user.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterAsync_ThrowsValidationException_WhenFullNameIsEmpty()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.IdentityService.Service(dbContext, _jwtServiceMock.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.JwtOptions()));
        var request = new Request.RegisterRequest { Email = "e@t.com", FullName = "", Password = "p", Role = "EXPERT" };

        // Act
        Func<Task> act = async () => await service.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RegisterAsync_ThrowsValidationException_WhenFullNameIsWhitespace()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.IdentityService.Service(dbContext, _jwtServiceMock.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.JwtOptions()));
        var request = new Request.RegisterRequest { Email = "e@t.com", FullName = "   ", Password = "p", Role = "EXPERT" };

        // Act
        Func<Task> act = async () => await service.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RefreshTokenAsync_Succeeds_WithValidToken()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.IdentityService.Service(dbContext, _jwtServiceMock.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.JwtOptions()));
        var refreshToken = "valid-rt";
        var user = new User { Email = "u@t.com", FullName = "U", PasswordHash = "h", RefreshToken = $"hash:{refreshToken}", RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(1), Role = UserRole.CLIENT };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        _jwtServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<string>())).Returns("new-at");
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("new-rt");

        // Act
        var result = await service.RefreshTokenAsync(new Request.RefreshTokenRequest { RefreshToken = refreshToken });

        // Assert
        result.AccessToken.Should().Be("new-at");
        result.RefreshToken.Should().Be("new-rt");
        user.RefreshToken.Should().Be("hash:new-rt");
    }

    [Fact]
    public async Task RefreshTokenAsync_ThrowsUnauthorized_WhenExpired()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.IdentityService.Service(dbContext, _jwtServiceMock.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.JwtOptions()));
        var refreshToken = "expired-rt";
        var user = new User { Email = "u@t.com", FullName = "U", PasswordHash = $"hash:{refreshToken}", RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(-1) };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await service.RefreshTokenAsync(new Request.RefreshTokenRequest { RefreshToken = refreshToken });

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task LoginAsync_Succeeds_WithCorrectCredentials()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.IdentityService.Service(dbContext, _jwtServiceMock.Object, Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.JwtOptions()));
        var email = "login@test.com";
        var password = "password123";
        var user = new User { Email = email, FullName = "Login User", PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), Role = UserRole.CLIENT };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        _jwtServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<string>())).Returns("at");
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("rt");

        // Act
        var result = await service.LoginAsync(new Request.LoginRequest { Email = email, Password = password });

        // Assert
        result.AccessToken.Should().Be("at");
    }
}
