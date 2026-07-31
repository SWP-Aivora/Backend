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
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            new Aivora.Services.RealtimeService.NullRealtimeService()
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

    private async Task<(Guid expertId, Guid milestoneId)> SeedSubmittableMilestone(AivoraDbContext dbContext)
    {
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        dbContext.Projects.Add(new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE });
        dbContext.Milestones.Add(new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.FUNDED, OrderIndex = 1 });
        await dbContext.SaveChangesAsync();
        return (expertId, milestoneId);
    }

    [Fact]
    public async Task SubmitDeliverableAsync_DescriptionTooLong_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var (expertId, milestoneId) = await SeedSubmittableMilestone(dbContext);
        var service = GetService(dbContext);
        var request = new Request.SubmitDeliverableRequest
        {
            Description = new string('a', 2001),
            FileUrl = "https://example.com/file.pdf"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.SubmitDeliverableAsync(expertId, milestoneId, request));
        ex.Message.Should().Be("Description must not exceed 2000 characters.");
    }

    [Fact]
    public async Task SubmitDeliverableAsync_InvalidUrl_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var (expertId, milestoneId) = await SeedSubmittableMilestone(dbContext);
        var service = GetService(dbContext);
        var request = new Request.SubmitDeliverableRequest
        {
            Description = "Done",
            FileUrl = "not a url at all"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.SubmitDeliverableAsync(expertId, milestoneId, request));
        ex.Message.Should().Be("FileUrl must be a valid http(s) URL.");
    }

    [Fact]
    public async Task SubmitDeliverableAsync_NonHttpScheme_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var (expertId, milestoneId) = await SeedSubmittableMilestone(dbContext);
        var service = GetService(dbContext);
        var request = new Request.SubmitDeliverableRequest
        {
            Description = "Done",
            DemoUrl = "ftp://example.com/demo"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.SubmitDeliverableAsync(expertId, milestoneId, request));
        ex.Message.Should().Be("DemoUrl must be a valid http(s) URL.");
    }

    [Fact]
    public async Task SubmitDeliverableAsync_ValidUrls_Succeeds()
    {
        // Arrange
        var dbContext = GetDbContext();
        var (expertId, milestoneId) = await SeedSubmittableMilestone(dbContext);
        var service = GetService(dbContext);
        var request = new Request.SubmitDeliverableRequest
        {
            Description = "Done",
            FileUrl = "https://example.com/file.pdf",
            SourceCodeUrl = "http://github.com/org/repo"
        };

        // Act
        var result = await service.SubmitDeliverableAsync(expertId, milestoneId, request);

        // Assert
        result.FileUrl.Should().Be("https://example.com/file.pdf");
        result.RevisionNumber.Should().Be(1);
    }

    [Fact]
    public async Task SubmitDeliverableAsync_PreviousMilestoneNotFinished_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestone2Id = Guid.NewGuid();

        dbContext.Projects.Add(new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE });
        dbContext.Milestones.Add(new Milestone { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.IN_PROGRESS, OrderIndex = 1 });
        dbContext.Milestones.Add(new Milestone { Id = milestone2Id, ProjectId = projectId, Title = "Milestone 2", Amount = 100, Status = MilestoneStatus.FUNDED, OrderIndex = 2 });
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.SubmitDeliverableRequest { Description = "Done", FileUrl = "https://example.com/file.pdf" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.SubmitDeliverableAsync(expertId, milestone2Id, request));
        ex.Message.Should().Be("Previous milestones must be completed before submitting a deliverable for this milestone.");
    }

    [Fact]
    public async Task SubmitDeliverableAsync_PreviousMilestoneRefunded_Succeeds()
    {
        // Arrange: a REFUNDED predecessor is terminal — it must not block later milestones
        var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestone2Id = Guid.NewGuid();

        dbContext.Projects.Add(new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE });
        dbContext.Milestones.Add(new Milestone { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.REFUNDED, OrderIndex = 1 });
        dbContext.Milestones.Add(new Milestone { Id = milestone2Id, ProjectId = projectId, Title = "Milestone 2", Amount = 100, Status = MilestoneStatus.FUNDED, OrderIndex = 2 });
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.SubmitDeliverableRequest { Description = "Done", FileUrl = "https://example.com/file.pdf" };

        // Act
        var result = await service.SubmitDeliverableAsync(expertId, milestone2Id, request);

        // Assert
        result.RevisionNumber.Should().Be(1);
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
