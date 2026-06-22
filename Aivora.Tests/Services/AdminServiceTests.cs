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
    public async Task SuspendUserAsync_Succeeds_WhenUserIsValid()
    {
        // Arrange
        var dbContext = GetDbContext();
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@test.com", FullName = "Test User", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE, PasswordHash = "x" };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>());

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

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>());

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

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>());

        // Act
        var result = await service.UnsuspendUserAsync(adminId, userId);

        // Assert
        result.Status.Should().Be(UserStatus.ACTIVE.ToString());
        var updatedUser = await dbContext.Users.FindAsync(userId);
        updatedUser!.Status.Should().Be(UserStatus.ACTIVE);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsCorrectStats()
    {
        // Arrange
        var dbContext = GetDbContext();
        
        // Add users
        dbContext.Users.Add(new User { Id = Guid.NewGuid(), Email = "client1@test.com", FullName = "Client 1", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE, PasswordHash = "x" });
        dbContext.Users.Add(new User { Id = Guid.NewGuid(), Email = "expert1@test.com", FullName = "Expert 1", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE, PasswordHash = "x" });
        
        // Add wallets with held balances
        dbContext.Wallets.Add(new Wallet { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), AvailableBalance = 100, HeldBalance = 1500 });
        dbContext.Wallets.Add(new Wallet { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), AvailableBalance = 200, HeldBalance = 2500 });
        
        // Add projects
        dbContext.Projects.Add(new Project { Id = Guid.NewGuid(), Title = "Project 1", Status = ProjectStatus.ACTIVE });
        dbContext.Projects.Add(new Project { Id = Guid.NewGuid(), Title = "Project 2", Status = ProjectStatus.PENDING_PAYMENT });
        
        // Add disputes
        dbContext.Disputes.Add(new Dispute { Id = Guid.NewGuid(), Status = DisputeStatus.OPEN, Reason = "Reason 1" });
        dbContext.Disputes.Add(new Dispute { Id = Guid.NewGuid(), Status = DisputeStatus.RESOLVED, Reason = "Reason 2" });

        await dbContext.SaveChangesAsync();

        var service = new AdminService(dbContext, Mock.Of<ILogger<AdminService>>());

        // Act
        var result = await service.GetDashboardStatsAsync();

        // Assert
        result.TotalUsers.Should().Be(2);
        result.TotalClients.Should().Be(1);
        result.TotalExperts.Should().Be(1);
        result.ActiveProjects.Should().Be(1);
        result.OpenDisputes.Should().Be(1);
        result.TotalSimulatedTransferAmount.Should().Be(4000); // 1500 + 2500
    }
}
