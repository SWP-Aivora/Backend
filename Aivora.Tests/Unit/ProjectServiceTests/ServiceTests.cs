using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ProjectService;
using Aivora.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using Xunit;
using Aivora.Services.ProjectService.Response;

namespace Aivora.Tests.Unit.ProjectServiceTests;

public class ServiceTests
{
    private readonly AivoraDbContext _dbContext;
    private readonly IService _service;

    public ServiceTests()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;
        _dbContext = new AivoraDbContext(options);

        _service = new Service(_dbContext);
    }

    [Fact]
    public async Task GetProjectByIdAsync_AdminUser_ShouldReturnProject()
    {
        // Arrange
        var project = TestData.CreateTestProject(Guid.NewGuid(), Guid.NewGuid());
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        var adminUserId = Guid.NewGuid();

        // Act
        var result = await _service.GetProjectByIdAsync(adminUserId, project.Id, UserRole.ADMIN);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(project.Id);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ClientUser_OwnProject_ShouldReturnProject()
    {
        // Arrange
        var clientUserId = Guid.NewGuid();
        var project = TestData.CreateTestProject(clientUserId, Guid.NewGuid());
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectByIdAsync(clientUserId, project.Id, UserRole.CLIENT);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(project.Id);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ExpertUser_OwnProject_ShouldReturnProject()
    {
        // Arrange
        var expertUserId = Guid.NewGuid();
        var clientUserId = Guid.NewGuid();
        var project = TestData.CreateTestProject(clientUserId, expertUserId);
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectByIdAsync(expertUserId, project.Id, UserRole.EXPERT);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(project.Id);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ClientUser_OtherProject_ShouldThrowUnauthorized()
    {
        // Arrange
        var clientUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var project = TestData.CreateTestProject(otherUserId, Guid.NewGuid());
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.GetProjectByIdAsync(clientUserId, project.Id, UserRole.CLIENT)
        );
    }

    [Fact]
    public async Task GetProjectByIdAsync_ExpertUser_UnassignedProject_ShouldThrowUnauthorized()
    {
        // Arrange
        var expertUserId = Guid.NewGuid();
        var clientUserId = Guid.NewGuid();
        var project = TestData.CreateTestProject(clientUserId, null); // No expert assigned
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.GetProjectByIdAsync(expertUserId, project.Id, UserRole.EXPERT)
        );
    }

    // NOTE: This test will fail until Service.GetProjectByIdAsync signature is changed
    [Fact]
    public async Task GetProjectByIdAsync_AdminUser_OwnProject_ShouldReturnProject()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var project = TestData.CreateTestProject(Guid.NewGuid(), Guid.NewGuid());
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectByIdAsync(adminUserId, project.Id, UserRole.ADMIN);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(project.Id);
    }
}