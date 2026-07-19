using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.DeliverableService;
using Aivora.Services.Exceptions;
using Aivora.Services.Treasury;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class DeliverableServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private Service GetService(AivoraDbContext dbContext)
    {
        return new Service(
            dbContext,
            Mock.Of<ITreasury>(),
            Mock.Of<Aivora.Services.NotificationService.IService>()
        );
    }

    [Fact]
    public async Task SubmitDeliverableAsync_ByNonExpert_ThrowsForbiddenException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.FUNDED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.SubmitDeliverableRequest { Description = "Done", FileUrl = "https://example.com/file.pdf" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.SubmitDeliverableAsync(outsiderId, milestoneId, request));
        ex.Message.Should().Be("Only the project expert can submit deliverables.");
    }

    [Fact]
    public async Task SubmitDeliverableAsync_WhenMilestoneDisputed_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.DISPUTED };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.DISPUTED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.SubmitDeliverableRequest { Description = "Done", FileUrl = "https://example.com/file.pdf" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.SubmitDeliverableAsync(expertId, milestoneId, request));
        ex.Message.Should().Be("Cannot submit a deliverable while there is an active dispute.");
    }

    [Fact]
    public async Task SubmitDeliverableAsync_WhenProjectDisputed_ThrowsValidationException()
    {
        // Arrange: milestone itself is FUNDED (normally submittable), but a sibling milestone made the project DISPUTED
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.DISPUTED };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.FUNDED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.SubmitDeliverableRequest { Description = "Done", FileUrl = "https://example.com/file.pdf" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.SubmitDeliverableAsync(expertId, milestoneId, request));
        ex.Message.Should().Be("Cannot submit a deliverable while there is an active dispute.");
    }

    [Fact]
    public async Task GetDeliverablesByMilestoneAsync_ByOutsider_ThrowsForbiddenException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.FUNDED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetDeliverablesByMilestoneAsync(outsiderId, milestoneId));
        ex.Message.Should().Be("Access denied.");
    }
}
