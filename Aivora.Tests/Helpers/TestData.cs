using Aivora.Repositories.Enums;
using Aivora.Repositories.Entities;
using System.Security.Claims;

namespace Aivora.Tests.Helpers;

public static class TestData
{
    public static Project CreateTestProject(Guid clientId, Guid? expertId = null)
    {
        return new Project
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ExpertId = expertId ?? Guid.Empty,
            Title = "Test Project",
            Status = ProjectStatus.PENDING_PAYMENT,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static User CreateTestUser(Guid userId, UserRole role)
    {
        return new User
        {
            Id = userId,
            Email = $"test-{role}@example.com",
            FullName = $"Test {role}",
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static ClaimsPrincipal CreateTestUserPrincipal(Guid userId, UserRole role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}