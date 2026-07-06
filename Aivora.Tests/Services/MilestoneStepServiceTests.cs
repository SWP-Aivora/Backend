using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.MilestoneService;
using Aivora.Services.Treasury;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class MilestoneStepServiceTests
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
    public async Task GetMilestoneStepsAsync_ReturnsOrderedList()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };

        var step1 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step B", OrderIndex = 2, Status = MilestoneStepStatus.PENDING };
        var step2 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step A", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };
        var step3 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step C", OrderIndex = 3, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.AddRange(step1, step2, step3);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Act
        var result = await service.GetMilestoneStepsAsync(clientId, milestoneId);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(3);
        result[0].OrderIndex.Should().Be(1);
        result[0].Title.Should().Be("Step A");
        result[1].OrderIndex.Should().Be(2);
        result[1].Title.Should().Be("Step B");
        result[2].OrderIndex.Should().Be(3);
        result[2].Title.Should().Be("Step C");
    }

    [Fact]
    public async Task AddMilestoneStepAsync_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.CreateMilestoneStepRequest
        {
            Title = "New Step",
            Description = "Step Description",
            OrderIndex = 1,
            DueDate = new DateOnly(2026, 7, 7)
        };

        // Act
        var result = await service.AddMilestoneStepAsync(clientId, milestoneId, request);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("New Step");
        result.Description.Should().Be("Step Description");
        result.OrderIndex.Should().Be(1);
        result.Status.Should().Be(MilestoneStepStatus.PENDING);
        result.DueDate.Should().Be(new DateOnly(2026, 7, 7));

        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == result.Id);
        dbStep.Should().NotBeNull();
        dbStep!.Title.Should().Be("New Step");
    }

    [Fact]
    public async Task UpdateMilestoneStepAsync_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Old Title", Description = "Old Desc", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.UpdateMilestoneStepRequest
        {
            Title = "Updated Title",
            Description = "Updated Desc",
            OrderIndex = 2,
            DueDate = new DateOnly(2026, 7, 8)
        };

        // Act
        var result = await service.UpdateMilestoneStepAsync(clientId, stepId, request);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Updated Title");
        result.Description.Should().Be("Updated Desc");
        result.OrderIndex.Should().Be(2);
        result.DueDate.Should().Be(new DateOnly(2026, 7, 8));

        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == stepId);
        dbStep!.Title.Should().Be("Updated Title");
        dbStep.Description.Should().Be("Updated Desc");
        dbStep.OrderIndex.Should().Be(2);
    }

    [Fact]
    public async Task DeleteMilestoneStepAsync_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Step to Delete", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Act
        await service.DeleteMilestoneStepAsync(clientId, stepId);

        // Assert
        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == stepId);
        dbStep.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStepStatusAsync_ExpertCompleted_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Step", OrderIndex = 1, Status = MilestoneStepStatus.IN_PROGRESS };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.COMPLETED };

        // Act
        var result = await service.UpdateStepStatusAsync(expertId, stepId, request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(MilestoneStepStatus.COMPLETED);
        result.CompletedAt.Should().NotBeNull();
        result.CompletedByUserId.Should().Be(expertId);

        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == stepId);
        dbStep!.Status.Should().Be(MilestoneStepStatus.COMPLETED);
        dbStep.CompletedAt.Should().NotBeNull();
        dbStep.CompletedByUserId.Should().Be(expertId);
    }

    [Fact]
    public async Task UpdateStepStatusAsync_ClientSkipped_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Step", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.SKIPPED };

        // Act
        var result = await service.UpdateStepStatusAsync(clientId, stepId, request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(MilestoneStepStatus.SKIPPED);

        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == stepId);
        dbStep!.Status.Should().Be(MilestoneStepStatus.SKIPPED);
    }

    [Fact]
    public async Task AddMilestoneStepAsync_ByNonClient_ThrowsUnauthorizedException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.CreateMilestoneStepRequest { Title = "Unauthorized Step", OrderIndex = 1 };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.AddMilestoneStepAsync(expertId, milestoneId, request));
    }

    [Fact]
    public async Task UpdateStepStatusAsync_InvalidTransition_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Completed Step", OrderIndex = 1, Status = MilestoneStepStatus.COMPLETED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.PENDING };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStepStatusAsync(expertId, stepId, request));
    }

    [Fact]
    public async Task ReorderMilestoneStepsAsync_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };

        var step1 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step 1", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };
        var step2 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step 2", OrderIndex = 2, Status = MilestoneStepStatus.PENDING };
        var step3 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step 3", OrderIndex = 3, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.AddRange(step1, step2, step3);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Let's reorder: step3 (index 1), step1 (index 2), step2 (index 3)
        var stepIds = new List<Guid> { step3.Id, step1.Id, step2.Id };

        // Act
        await service.ReorderMilestoneStepsAsync(clientId, milestoneId, stepIds);

        // Assert
        var dbStep1 = await dbContext.MilestoneSteps.FindAsync(step1.Id);
        var dbStep2 = await dbContext.MilestoneSteps.FindAsync(step2.Id);
        var dbStep3 = await dbContext.MilestoneSteps.FindAsync(step3.Id);

        dbStep3!.OrderIndex.Should().Be(1);
        dbStep1!.OrderIndex.Should().Be(2);
        dbStep2!.OrderIndex.Should().Be(3);
    }

    [Fact]
    public async Task ReorderMilestoneStepsAsync_ByNonClient_ThrowsUnauthorizedException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100 };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.ReorderMilestoneStepsAsync(expertId, milestoneId, new List<Guid>()));
    }

    [Theory]
    [InlineData(ProjectStatus.COMPLETED)]
    [InlineData(ProjectStatus.CANCELLED)]
    public async Task ModifyStep_WhenProjectCompletedOrCancelled_ThrowsValidationException(ProjectStatus projectStatus)
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = projectStatus };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.CREATED };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Step 1", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Act & Assert for UpdateMilestoneStepAsync
        var updateEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneStepAsync(clientId, stepId, new Request.UpdateMilestoneStepRequest { Title = "New Title" }));
        updateEx.Message.Should().Be("Cannot modify steps in a completed or cancelled project.");

        // Act & Assert for DeleteMilestoneStepAsync
        var deleteEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeleteMilestoneStepAsync(clientId, stepId));
        deleteEx.Message.Should().Be("Cannot modify steps in a completed or cancelled project.");

        // Act & Assert for ReorderMilestoneStepsAsync
        var reorderEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(clientId, milestoneId, new List<Guid> { stepId }));
        reorderEx.Message.Should().Be("Cannot modify steps in a completed or cancelled project.");

        // Act & Assert for UpdateStepStatusAsync
        var statusEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStepStatusAsync(clientId, stepId, new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.SKIPPED }));
        statusEx.Message.Should().Be("Cannot modify steps in a completed or cancelled project.");
    }

    [Theory]
    [InlineData(MilestoneStatus.APPROVED)]
    [InlineData(MilestoneStatus.RELEASED)]
    [InlineData(MilestoneStatus.COMPLETED)]
    [InlineData(MilestoneStatus.REFUNDED)]
    public async Task ModifyStep_WhenMilestoneFinalized_ThrowsValidationException(MilestoneStatus milestoneStatus)
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = milestoneStatus };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Step 1", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Act & Assert for UpdateMilestoneStepAsync
        var updateEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneStepAsync(clientId, stepId, new Request.UpdateMilestoneStepRequest { Title = "New Title" }));
        updateEx.Message.Should().Be("Cannot modify steps for a finalized milestone.");

        // Act & Assert for DeleteMilestoneStepAsync
        var deleteEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeleteMilestoneStepAsync(clientId, stepId));
        deleteEx.Message.Should().Be("Cannot modify steps for a finalized milestone.");

        // Act & Assert for ReorderMilestoneStepsAsync
        var reorderEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(clientId, milestoneId, new List<Guid> { stepId }));
        reorderEx.Message.Should().Be("Cannot modify steps for a finalized milestone.");

        // Act & Assert for UpdateStepStatusAsync
        var statusEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStepStatusAsync(clientId, stepId, new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.SKIPPED }));
        statusEx.Message.Should().Be("Cannot modify steps for a finalized milestone.");
    }

    [Fact]
    public async Task ReorderMilestoneStepsAsync_WithMissingStepIds_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.CREATED };

        var step1 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step 1", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };
        var step2 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step 2", OrderIndex = 2, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.AddRange(step1, step2);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Only pass step1 ID (missing step2 ID)
        var stepIds = new List<Guid> { step1.Id };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(clientId, milestoneId, stepIds));
        ex.Message.Should().Be("All step IDs must be provided for reordering.");
    }

    [Theory]
    [InlineData(MilestoneStatus.APPROVED)]
    [InlineData(MilestoneStatus.RELEASED)]
    [InlineData(MilestoneStatus.COMPLETED)]
    [InlineData(MilestoneStatus.REFUNDED)]
    public async Task AddMilestoneStepAsync_WhenMilestoneFinalized_ThrowsValidationException(MilestoneStatus milestoneStatus)
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = milestoneStatus };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.CreateMilestoneStepRequest
        {
            Title = "New Step",
            OrderIndex = 1
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddMilestoneStepAsync(clientId, milestoneId, request));
        ex.Message.Should().Be("Cannot add steps to a finalized milestone.");
    }

    [Fact]
    public async Task ReorderMilestoneStepsAsync_WithMismatchedStepIds_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.CREATED };

        var step1 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step 1", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };
        var step2 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step 2", OrderIndex = 2, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.AddRange(step1, step2);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Pass 2 IDs: step1 ID and a random Guid (so count matches but set doesn't match)
        var stepIds = new List<Guid> { step1.Id, Guid.NewGuid() };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(clientId, milestoneId, stepIds));
        ex.Message.Should().Be("All step IDs must be provided for reordering.");
    }

    [Fact]
    public async Task ReorderMilestoneStepsAsync_WithDuplicateStepIds_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.CREATED };

        var step1 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step 1", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };
        var step2 = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = milestoneId, Title = "Step 2", OrderIndex = 2, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.AddRange(step1, step2);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Pass duplicate step1 ID (count is 2, all exist, but it's a duplicate)
        var stepIds = new List<Guid> { step1.Id, step1.Id };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(clientId, milestoneId, stepIds));
        ex.Message.Should().Be("All step IDs must be provided for reordering.");
    }
}

