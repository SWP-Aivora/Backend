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
    public async Task GetExpertProfileAsync_IncludesSkills()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var skill = new Skill { Id = Guid.NewGuid(), Name = "C#" };

        dbContext.Users.Add(new User { Id = userId, FullName = "Expert Name", Email = "e2@t.com", Role = UserRole.EXPERT, PasswordHash = "h" });
        dbContext.ExpertProfiles.Add(new ExpertProfile { Id = profileId, UserId = userId, Title = "Expert" });
        dbContext.Skills.Add(skill);
        dbContext.ExpertSkills.Add(new ExpertSkill { ExpertId = profileId, SkillId = skill.Id, Level = SkillLevel.EXPERT });
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.ProfileService.Service(dbContext);

        // Act
        var result = await service.GetExpertProfileAsync(userId);

        // Assert
        result.Skills.Should().ContainSingle(s => s.SkillId == skill.Id && s.SkillName == "C#");
    }

    [Fact]
    public async Task GetPublicExpertProfileAsync_IncludesSkills()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var skill = new Skill { Id = Guid.NewGuid(), Name = "Python" };

        dbContext.Users.Add(new User { Id = userId, FullName = "Expert Name", Email = "e3@t.com", Role = UserRole.EXPERT, PasswordHash = "h" });
        dbContext.ExpertProfiles.Add(new ExpertProfile { Id = profileId, UserId = userId, Title = "Expert" });
        dbContext.Skills.Add(skill);
        dbContext.ExpertSkills.Add(new ExpertSkill { ExpertId = profileId, SkillId = skill.Id, Level = SkillLevel.EXPERT });
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.ProfileService.Service(dbContext);

        // Act
        var result = await service.GetPublicExpertProfileAsync(userId);

        // Assert
        result.Skills.Should().ContainSingle(s => s.SkillId == skill.Id && s.SkillName == "Python");
    }

    [Fact]
    public async Task GetPublicExpertProfileAsync_WithCompletedProjects_ReturnsRealTimeCount()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        dbContext.Users.Add(new User { Id = userId, FullName = "Expert Name", Email = "e4@t.com", Role = UserRole.EXPERT, PasswordHash = "h" });
        dbContext.Users.Add(new User { Id = clientId, FullName = "Client Name", Email = "c4@t.com", Role = UserRole.CLIENT, PasswordHash = "h" });
        // Stale seeded value deliberately different from the real completed count below,
        // so this test fails against the pre-fix code (which just returns this field).
        dbContext.ExpertProfiles.Add(new ExpertProfile { Id = Guid.NewGuid(), UserId = userId, Title = "Expert", CompletedProjects = 15 });
        dbContext.Projects.AddRange(
            new Project { Id = Guid.NewGuid(), ClientId = clientId, ExpertId = userId, Title = "P1", Description = "D", Status = ProjectStatus.COMPLETED },
            new Project { Id = Guid.NewGuid(), ClientId = clientId, ExpertId = userId, Title = "P2", Description = "D", Status = ProjectStatus.COMPLETED },
            new Project { Id = Guid.NewGuid(), ClientId = clientId, ExpertId = userId, Title = "P3", Description = "D", Status = ProjectStatus.ACTIVE },
            new Project { Id = Guid.NewGuid(), ClientId = clientId, ExpertId = userId, Title = "P4", Description = "D", Status = ProjectStatus.CANCELLED }
        );
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.ProfileService.Service(dbContext);

        // Act
        var result = await service.GetPublicExpertProfileAsync(userId);

        // Assert
        result.CompletedProjects.Should().Be(2);
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

    [Fact]
    public async Task GetPublicCompletedProjectsAsync_ReturnsOnlyCompletedProjects_AndMapsReview()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var client = new User
        {
            Id = clientId,
            FullName = "Client Display Name",
            Email = "client@test.com",
            Role = UserRole.CLIENT,
            PasswordHash = "hash"
        };

        var expert = new User
        {
            Id = expertId,
            FullName = "Expert Name",
            Email = "expert@test.com",
            Role = UserRole.EXPERT,
            PasswordHash = "hash"
        };

        var expertProfile = new ExpertProfile
        {
            Id = Guid.NewGuid(),
            UserId = expertId,
            Title = "AI Specialist"
        };

        var completedProject = new Project
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ExpertId = expertId,
            Title = "Completed Project",
            Description = "Completed Project Summary",
            Status = ProjectStatus.COMPLETED
        };

        var activeProject = new Project
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ExpertId = expertId,
            Title = "Active Project",
            Description = "Active Project Summary",
            Status = ProjectStatus.ACTIVE
        };

        var cancelledProject = new Project
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ExpertId = expertId,
            Title = "Cancelled Project",
            Description = "Cancelled Project Summary",
            Status = ProjectStatus.CANCELLED
        };

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ProjectId = completedProject.Id,
            ReviewerId = clientId,
            RevieweeId = expertId,
            Rating = 5,
            Comment = "Excellent"
        };

        dbContext.Users.AddRange(client, expert);
        dbContext.ExpertProfiles.Add(expertProfile);
        dbContext.Projects.AddRange(completedProject, activeProject, cancelledProject);
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.ProfileService.Service(dbContext);

        // Act
        var result = await service.GetPublicCompletedProjectsAsync(expertId);

        // Assert
        result.Projects.Should().HaveCount(1);
        var projectSummary = result.Projects[0];
        projectSummary.Title.Should().Be("Completed Project");
        projectSummary.Summary.Should().Be("Completed Project Summary");
        projectSummary.Rating.Should().Be(5);
        projectSummary.ReviewComment.Should().Be("Excellent");
        projectSummary.ClientDisplayName.Should().Be("Client Display Name");
    }

    [Fact]
    public async Task GetPublicCompletedProjectsAsync_SupportsPagination()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var client = new User
        {
            Id = clientId,
            FullName = "Client Name",
            Email = "client@test.com",
            Role = UserRole.CLIENT,
            PasswordHash = "hash"
        };

        var expert = new User
        {
            Id = expertId,
            FullName = "Expert Name",
            Email = "expert@test.com",
            Role = UserRole.EXPERT,
            PasswordHash = "hash"
        };

        var expertProfile = new ExpertProfile
        {
            Id = Guid.NewGuid(),
            UserId = expertId,
            Title = "AI Specialist"
        };

        dbContext.Users.AddRange(client, expert);
        dbContext.ExpertProfiles.Add(expertProfile);

        for (int i = 1; i <= 15; i++)
        {
            dbContext.Projects.Add(new Project
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                ExpertId = expertId,
                Title = $"Completed Project {i}",
                Description = $"Summary {i}",
                Status = ProjectStatus.COMPLETED
            });
        }

        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.ProfileService.Service(dbContext);

        // Act
        var result = await service.GetPublicCompletedProjectsAsync(expertId, page: 2, pageSize: 10);

        // Assert
        result.Projects.Count.Should().Be(5);
        result.TotalCount.Should().Be(15);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetPublicCompletedProjectsAsync_ThrowsNotFoundException_WhenExpertNotFound()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.ProfileService.Service(dbContext);
        var randomExpertId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await service.GetPublicCompletedProjectsAsync(randomExpertId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

