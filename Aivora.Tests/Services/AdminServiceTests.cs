using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AdminService;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class AdminServiceTests
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
    public async Task GetDashboardStatsAsync_CountsOpenAndUnderReviewDisputes()
    {
        // Arrange
        var dbContext = GetDbContext();
        dbContext.Disputes.AddRange(
            new Dispute { ProjectId = Guid.NewGuid(), MilestoneId = Guid.NewGuid(), PaymentId = Guid.NewGuid(), OpenedBy = Guid.NewGuid(), AgainstUserId = Guid.NewGuid(), Reason = "Quality", Status = DisputeStatus.OPEN },
            new Dispute { ProjectId = Guid.NewGuid(), MilestoneId = Guid.NewGuid(), PaymentId = Guid.NewGuid(), OpenedBy = Guid.NewGuid(), AgainstUserId = Guid.NewGuid(), Reason = "Scope", Status = DisputeStatus.UNDER_REVIEW },
            new Dispute { ProjectId = Guid.NewGuid(), MilestoneId = Guid.NewGuid(), PaymentId = Guid.NewGuid(), OpenedBy = Guid.NewGuid(), AgainstUserId = Guid.NewGuid(), Reason = "Old", Status = DisputeStatus.RESOLVED },
            new Dispute { ProjectId = Guid.NewGuid(), MilestoneId = Guid.NewGuid(), PaymentId = Guid.NewGuid(), OpenedBy = Guid.NewGuid(), AgainstUserId = Guid.NewGuid(), Reason = "Closed", Status = DisputeStatus.CLOSED }
        );
        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());

        // Act
        var result = await service.GetDashboardStatsAsync();

        // Assert
        // UNDER_REVIEW disputes still leave the project/milestone marked DISPUTED (only Resolve/Close revert
        // it), so the dashboard's count must include them to stay consistent with Project Management's view.
        result.OpenDisputes.Should().Be(2);
    }

    [Fact]
    public async Task GetUserByIdAsync_ReturnsUser_WhenUserExists()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@test.com", FullName = "Test User", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE, PasswordHash = "x" };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());

        // Act
        var result = await service.GetUserByIdAsync(userId);

        // Assert
        result.Id.Should().Be(userId);
        result.Email.Should().Be("test@test.com");
    }

    [Fact]
    public async Task GetUserByIdAsync_ThrowsNotFoundException_WhenUserDoesNotExist()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetUserByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SuspendUserAsync_Succeeds_WhenUserIsValid()
    {
        // Arrange
        var dbContext = GetDbContext();
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@test.com", FullName = "Test User", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE, PasswordHash = "x" };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());

        // Act
        var result = await service.SuspendUserAsync(adminId, userId, "Rules violation");

        // Assert
        result.Status.Should().Be(UserStatus.SUSPENDED.ToString());
        var updatedUser = await dbContext.Users.FindAsync(userId);
        updatedUser!.Status.Should().Be(UserStatus.SUSPENDED);
    }

    [Fact]
    public async Task SuspendUserAsync_ThrowsValidationException_WhenUserIsAdmin()
    {
        // Arrange
        var dbContext = GetDbContext();
        var adminId = Guid.NewGuid();
        var targetAdminId = Guid.NewGuid();
        var targetAdmin = new User { Id = targetAdminId, Email = "admin2@test.com", FullName = "Admin 2", Role = UserRole.ADMIN, Status = UserStatus.ACTIVE, PasswordHash = "x" };
        dbContext.Users.Add(targetAdmin);
        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.SuspendUserAsync(adminId, targetAdminId, "Cannot suspend admin"));
    }

    [Fact]
    public async Task UnsuspendUserAsync_Succeeds_WhenUserIsSuspended()
    {
        // Arrange
        var dbContext = GetDbContext();
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@test.com", FullName = "Test User", Role = UserRole.CLIENT, Status = UserStatus.SUSPENDED, PasswordHash = "x" };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());

        // Act
        var result = await service.UnsuspendUserAsync(adminId, userId);

        // Assert
        result.Status.Should().Be(UserStatus.ACTIVE.ToString());
        var updatedUser = await dbContext.Users.FindAsync(userId);
        updatedUser!.Status.Should().Be(UserStatus.ACTIVE);
    }

    [Fact]
    public async Task ReviewExpertProfileUpdateAsync_Succeeds_WhenApproved()
    {
        // Arrange
        var dbContext = GetDbContext();
        var adminId = Guid.NewGuid();
        var expertProfileId = Guid.NewGuid();
        var updateId = Guid.NewGuid();

        var expertUserId = Guid.NewGuid();
        var expertUser = new User { Id = expertUserId, Email = "expert@test.com", FullName = "Expert Name", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE, PasswordHash = "x" };
        var profile = new ExpertProfile { Id = expertProfileId, Title = "Old Title", ExperienceYears = 5, UserId = expertUserId };
        var update = new ExpertProfileUpdate
        {
            Id = updateId,
            ExpertProfileId = expertProfileId,
            Title = "New Title",
            ExperienceYears = 7,
            Status = ProfileUpdateStatus.PENDING
        };

        dbContext.Users.Add(expertUser);
        dbContext.ExpertProfiles.Add(profile);
        dbContext.ExpertProfileUpdates.Add(update);
        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());
        var request = new Aivora.Services.AdminService.Request.ReviewExpertProfileUpdateRequest { IsApproved = true };

        // Act
        var result = await service.ReviewExpertProfileUpdateAsync(adminId, updateId, request);

        // Assert
        result.Status.Should().Be(ProfileUpdateStatus.APPROVED.ToString());
        result.ExpertId.Should().Be(expertUserId);
        result.FullName.Should().Be("Expert Name");
        result.CurrentTitle.Should().Be("Old Title");
        result.CurrentExperienceYears.Should().Be(5);

        var updatedProfile = await dbContext.ExpertProfiles.FindAsync(expertProfileId);
        updatedProfile!.Title.Should().Be("New Title");
        updatedProfile.ExperienceYears.Should().Be(7);
    }

    [Fact]
    public async Task GetExpertProfileUpdateByIdAsync_ReturnsIdentityAndCurrentValues()
    {
        // Arrange
        var dbContext = GetDbContext();
        var expertProfileId = Guid.NewGuid();
        var updateId = Guid.NewGuid();
        var expertUserId = Guid.NewGuid();

        var expertUser = new User { Id = expertUserId, Email = "expert3@test.com", FullName = "Expert Name", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE, PasswordHash = "x" };
        var profile = new ExpertProfile { Id = expertProfileId, Title = "Old Title", Bio = "Old Bio", HourlyRate = 20, ExperienceYears = 5, UserId = expertUserId };
        var update = new ExpertProfileUpdate
        {
            Id = updateId,
            ExpertProfileId = expertProfileId,
            Title = "New Title",
            ExperienceYears = 7,
            Status = ProfileUpdateStatus.PENDING
        };

        dbContext.Users.Add(expertUser);
        dbContext.ExpertProfiles.Add(profile);
        dbContext.ExpertProfileUpdates.Add(update);
        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());

        // Act
        var result = await service.GetExpertProfileUpdateByIdAsync(updateId);

        // Assert
        result.ExpertId.Should().Be(expertUserId);
        result.Email.Should().Be("expert3@test.com");
        result.Title.Should().Be("New Title");
        result.CurrentTitle.Should().Be("Old Title");
        result.CurrentBio.Should().Be("Old Bio");
        result.CurrentHourlyRate.Should().Be(20);
        result.CurrentExperienceYears.Should().Be(5);
    }

    [Fact]
    public async Task GetExpertProfileUpdateByIdAsync_ThrowsNotFound_WhenMissing()
    {
        var dbContext = GetDbContext();
        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetExpertProfileUpdateByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReviewExpertProfileUpdateAsync_Succeeds_WhenRejected()
    {
        // Arrange
        var dbContext = GetDbContext();
        var adminId = Guid.NewGuid();
        var expertProfileId = Guid.NewGuid();
        var updateId = Guid.NewGuid();

        var expertUserId = Guid.NewGuid();
        var expertUser = new User { Id = expertUserId, Email = "expert2@test.com", FullName = "Expert Name", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE, PasswordHash = "x" };
        var profile = new ExpertProfile { Id = expertProfileId, Title = "Old Title", ExperienceYears = 5, UserId = expertUserId };
        var update = new ExpertProfileUpdate
        {
            Id = updateId,
            ExpertProfileId = expertProfileId,
            Title = "New Title",
            ExperienceYears = 7,
            Status = ProfileUpdateStatus.PENDING
        };

        dbContext.Users.Add(expertUser);
        dbContext.ExpertProfiles.Add(profile);
        dbContext.ExpertProfileUpdates.Add(update);
        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>(), Mock.Of<Aivora.Services.NotificationService.IService>());
        var request = new Aivora.Services.AdminService.Request.ReviewExpertProfileUpdateRequest { IsApproved = false, RejectionReason = "Invalid info" };

        // Act
        var result = await service.ReviewExpertProfileUpdateAsync(adminId, updateId, request);

        // Assert
        result.Status.Should().Be(ProfileUpdateStatus.REJECTED.ToString());
        result.RejectionReason.Should().Be("Invalid info");

        var updatedProfile = await dbContext.ExpertProfiles.FindAsync(expertProfileId);
        updatedProfile!.Title.Should().Be("Old Title"); // Unchanged
        updatedProfile.ExperienceYears.Should().Be(5);
    }
}
