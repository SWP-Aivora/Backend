using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ProjectService;
using Aivora.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.Unit.ProjectServiceTests;

public class ServiceTests : IDisposable
{
    private readonly AivoraDbContext _dbContext;
    private readonly IService _service;

    public ServiceTests()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AivoraDbContext(options);

        _service = new Service(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    /// <summary>
    /// Helper: tạo Project kèm User entities cần thiết cho Include() navigation properties.
    /// </summary>
    private async Task SeedProjectAsync(Guid clientId, Guid expertId, Guid projectId)
    {
        // Tạo User entities để Include() không bị lỗi với InMemory provider
        var client = new User
        {
            Id = clientId,
            Email = $"client-{clientId}@test.com",
            FullName = "Test Client",
            Role = UserRole.CLIENT,
            PasswordHash = "hash"
        };
        var expert = new User
        {
            Id = expertId,
            Email = $"expert-{expertId}@test.com",
            FullName = "Test Expert",
            Role = UserRole.EXPERT,
            PasswordHash = "hash"
        };

        _dbContext.Users.Add(client);
        _dbContext.Users.Add(expert);

        var project = new Project
        {
            Id = projectId,
            ClientId = clientId,
            ExpertId = expertId,
            Title = "Test Project",
            Status = ProjectStatus.ACTIVE
        };
        _dbContext.Projects.Add(project);

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetProjectByIdAsync_AdminUser_ShouldReturnProject()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientId, expertId, projectId);
        var adminUserId = Guid.NewGuid();

        // Act
        var result = await _service.GetProjectByIdAsync(adminUserId, projectId, UserRole.ADMIN);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ClientUser_OwnProject_ShouldReturnProject()
    {
        // Arrange
        var clientUserId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientUserId, expertId, projectId);

        // Act
        var result = await _service.GetProjectByIdAsync(clientUserId, projectId, UserRole.CLIENT);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ExpertUser_OwnProject_ShouldReturnProject()
    {
        // Arrange
        var clientUserId = Guid.NewGuid();
        var expertUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientUserId, expertUserId, projectId);

        // Act
        var result = await _service.GetProjectByIdAsync(expertUserId, projectId, UserRole.EXPERT);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ClientUser_OtherProject_ShouldThrowUnauthorized()
    {
        // Arrange
        var ownerClientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(ownerClientId, expertId, projectId);

        var otherClientId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.GetProjectByIdAsync(otherClientId, projectId, UserRole.CLIENT)
        );
    }

    [Fact]
    public async Task GetProjectByIdAsync_ExpertUser_UnassignedProject_ShouldThrowUnauthorized()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var assignedExpertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientId, assignedExpertId, projectId);

        var differentExpertId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.GetProjectByIdAsync(differentExpertId, projectId, UserRole.EXPERT)
        );
    }

    [Fact]
    public async Task GetProjectByIdAsync_AdminUser_OwnProject_ShouldReturnProject()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientId, expertId, projectId);
        var adminUserId = Guid.NewGuid();

        // Act
        var result = await _service.GetProjectByIdAsync(adminUserId, projectId, UserRole.ADMIN);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
    }
}
