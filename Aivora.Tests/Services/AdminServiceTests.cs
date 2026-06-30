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
}
