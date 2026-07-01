using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.ProfileService;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests.Services;

public class ProfileServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    [Fact]
    public async Task UpdateUserAsync_UpdatesFields_Correctly()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FullName = "Old Name", Email = "u@t.com", Role = UserRole.CLIENT, PasswordHash = "h" };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.ProfileService.Service(dbContext);
        var request = new Request.UpdateUserRequest { FullName = "New Name", Phone = "123456" };

        // Act
        var result = await service.UpdateUserAsync(userId, request);

        // Assert
        result.FullName.Should().Be("New Name");
        result.Phone.Should().Be("123456");
        var dbUser = await dbContext.Users.FindAsync(userId);
        dbUser!.FullName.Should().Be("New Name");
    }

    [Fact]
    public async Task GetPublicExpertProfileAsync_ReturnsProfile_ByUserIdOrProfileId()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FullName = "Expert Name", Email = "e@t.com", Role = UserRole.EXPERT, PasswordHash = "h" };
        var profile = new ExpertProfile { Id = Guid.NewGuid(), UserId = userId, Title = "Expert" };

        dbContext.Users.Add(user);
        dbContext.ExpertProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.ProfileService.Service(dbContext);

        // Act
        var resultByUserId = await service.GetPublicExpertProfileAsync(userId);
        var resultByProfileId = await service.GetPublicExpertProfileAsync(profile.Id);

        // Assert
        resultByUserId.Title.Should().Be("Expert");
        resultByUserId.FullName.Should().Be("Expert Name");
        resultByProfileId.Title.Should().Be("Expert");
        resultByProfileId.FullName.Should().Be("Expert Name");
    }

    [Fact]
    public async Task GetClientProfileAsync_ReturnsProfile_WhenExists()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var profile = new ClientProfile { UserId = userId, CompanyName = "Test Co" };
        dbContext.ClientProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.ProfileService.Service(dbContext);

        // Act
        var result = await service.GetClientProfileAsync(userId);

        // Assert
        result.CompanyName.Should().Be("Test Co");
    }

    [Fact]
    public async Task UpdateExpertProfileAsync_CreatesPendingUpdate_InsteadOfImmediateChange()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        
        var user = new User { Id = userId, FullName = "Expert Name", Email = "e@t.com", Role = UserRole.EXPERT, PasswordHash = "h" };
        var profile = new ExpertProfile { Id = profileId, UserId = userId, Title = "Old Title", ExperienceYears = 5 };

        dbContext.Users.Add(user);
        dbContext.ExpertProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.ProfileService.Service(dbContext);
        var request = new Request.UpdateExpertProfileRequest 
        { 
            Title = "New Title",
            Bio = "New Bio",
            HourlyRate = 50,
            ExperienceYears = 10
        };

        // Act
        var result = await service.UpdateExpertProfileAsync(userId, request);

        // Assert
        // The returned profile should still have old data since it's pending
        result.Title.Should().Be("Old Title");
        
        // A pending update should have been created
        var pendingUpdate = await dbContext.ExpertProfileUpdates.FirstOrDefaultAsync(u => u.ExpertProfileId == profileId);
        pendingUpdate.Should().NotBeNull();
        pendingUpdate!.Title.Should().Be("New Title");
        pendingUpdate.ExperienceYears.Should().Be(10);
        pendingUpdate.Status.Should().Be(ProfileUpdateStatus.PENDING);
    }
}
