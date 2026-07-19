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

    private Service GetService(AivoraDbContext dbContext, Aivora.Services.NotificationService.IService? notificationService = null)
    {
        return new Service(
            dbContext,
            Mock.Of<ITreasury>(),
            notificationService ?? Mock.Of<Aivora.Services.NotificationService.IService>(),
            Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>()
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
        var result = await service.AddMilestoneStepAsync(expertId, milestoneId, request);

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
        var result = await service.UpdateMilestoneStepAsync(expertId, stepId, request);

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
    public async Task UpdateMilestoneStepAsync_NullableFieldsHandling()
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
        var step = new MilestoneStep
        {
            Id = stepId,
            MilestoneId = milestoneId,
            Title = "Original Title",
            Description = "Original Description",
            DueDate = new DateOnly(2026, 7, 7),
            OrderIndex = 1,
            Status = MilestoneStepStatus.PENDING
        };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Case 1: Unspecified fields (Description and DueDate are omitted/default null but IsSet is false)
        var requestNoChange = new Request.UpdateMilestoneStepRequest
        {
            Title = "New Title Only"
        };
        var result1 = await service.UpdateMilestoneStepAsync(expertId, stepId, requestNoChange);
        result1.Title.Should().Be("New Title Only");
        result1.Description.Should().Be("Original Description"); // preserved
        result1.DueDate.Should().Be(new DateOnly(2026, 7, 7)); // preserved

        // Case 2: Explicitly setting fields to null
        var requestNullify = new Request.UpdateMilestoneStepRequest
        {
            Description = null,
            DueDate = null
        };
        var result2 = await service.UpdateMilestoneStepAsync(expertId, stepId, requestNullify);
        result2.Description.Should().BeNull(); // cleared
        result2.DueDate.Should().BeNull(); // cleared

        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == stepId);
        dbStep!.Description.Should().BeNull();
        dbStep.DueDate.Should().BeNull();
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
        await service.DeleteMilestoneStepAsync(expertId, stepId);

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
    public async Task UpdateStepStatusAsync_ExpertSkipped_Success()
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
        var result = await service.UpdateStepStatusAsync(expertId, stepId, request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(MilestoneStepStatus.SKIPPED);

        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == stepId);
        dbStep!.Status.Should().Be(MilestoneStepStatus.SKIPPED);
    }

    [Fact]
    public async Task AddMilestoneStepAsync_ByClient_ThrowsUnauthorizedException()
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
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.AddMilestoneStepAsync(clientId, milestoneId, request));
        ex.Message.Should().Be("Only the expert can manage milestone steps.");
    }

    [Fact]
    public async Task UpdateMilestoneStepAsync_ByClient_ThrowsUnauthorizedException()
    {
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
        var request = new Request.UpdateMilestoneStepRequest { Title = "Updated" };

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.UpdateMilestoneStepAsync(clientId, stepId, request));
        ex.Message.Should().Be("Only the expert can manage milestone steps.");
    }

    [Fact]
    public async Task DeleteMilestoneStepAsync_ByClient_ThrowsUnauthorizedException()
    {
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

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.DeleteMilestoneStepAsync(clientId, stepId));
        ex.Message.Should().Be("Only the expert can manage milestone steps.");
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
    public async Task UpdateStepStatusAsync_ClientSkip_ThrowsUnauthorizedException()
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

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.UpdateStepStatusAsync(clientId, stepId, request));
        ex.Message.Should().Be("Only the expert can manage milestone steps.");
    }

    [Fact]
    public async Task UpdateStepStatusAsync_ExpertBlockWithReason_Success()
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
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.BLOCKED, Reason = "Waiting on client access" };

        // Act
        var result = await service.UpdateStepStatusAsync(expertId, stepId, request);

        // Assert
        result.Status.Should().Be(MilestoneStepStatus.BLOCKED);
        result.BlockedReason.Should().Be("Waiting on client access");

        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == stepId);
        dbStep!.Status.Should().Be(MilestoneStepStatus.BLOCKED);
        dbStep.BlockedReason.Should().Be("Waiting on client access");
    }

    [Fact]
    public async Task UpdateStepStatusAsync_ExpertBlock_NotificationFailure_StillPersistsStatus()
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

        var notificationMock = new Mock<Aivora.Services.NotificationService.IService>();
        notificationMock
            .Setup(n => n.SendNotificationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Simulated notification outage"));
        var service = GetService(dbContext, notificationMock.Object);

        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.BLOCKED, Reason = "Waiting on client access" };

        // Act
        var result = await service.UpdateStepStatusAsync(expertId, stepId, request);

        // Assert
        result.Status.Should().Be(MilestoneStepStatus.BLOCKED);
        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == stepId);
        dbStep!.Status.Should().Be(MilestoneStepStatus.BLOCKED);
        notificationMock.Verify(n => n.SendNotificationAsync(clientId, It.IsAny<string>(), It.Is<string>(m => m.Contains("Waiting on client access")), "MILESTONE", It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStepStatusAsync_ExpertBlockWithoutReason_ThrowsValidationException()
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
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.BLOCKED };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStepStatusAsync(expertId, stepId, request));
    }

    [Fact]
    public async Task UpdateStepStatusAsync_ClientBlock_ThrowsUnauthorizedException()
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
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.BLOCKED, Reason = "reason" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.UpdateStepStatusAsync(clientId, stepId, request));
        ex.Message.Should().Be("Only the expert can manage milestone steps.");
    }

    [Fact]
    public async Task UpdateStepStatusAsync_BlockPendingStep_ThrowsValidationException()
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
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.BLOCKED, Reason = "reason" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStepStatusAsync(expertId, stepId, request));
    }

    [Fact]
    public async Task UpdateStepStatusAsync_ClientUnblock_Success()
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
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Step", OrderIndex = 1, Status = MilestoneStepStatus.BLOCKED, BlockedReason = "Waiting on access" };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.IN_PROGRESS };

        // Act
        var result = await service.UpdateStepStatusAsync(clientId, stepId, request);

        // Assert
        result.Status.Should().Be(MilestoneStepStatus.IN_PROGRESS);
        result.BlockedReason.Should().BeNull();

        var dbStep = await dbContext.MilestoneSteps.FirstOrDefaultAsync(s => s.Id == stepId);
        dbStep!.Status.Should().Be(MilestoneStepStatus.IN_PROGRESS);
        dbStep.BlockedReason.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStepStatusAsync_ExpertUnblock_ThrowsUnauthorizedException()
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
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Step", OrderIndex = 1, Status = MilestoneStepStatus.BLOCKED, BlockedReason = "Waiting on access" };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.IN_PROGRESS };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.UpdateStepStatusAsync(expertId, stepId, request));
        ex.Message.Should().Be("Only the client can unblock a step.");
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
        await service.ReorderMilestoneStepsAsync(expertId, milestoneId, stepIds);

        // Assert
        var dbStep1 = await dbContext.MilestoneSteps.FindAsync(step1.Id);
        var dbStep2 = await dbContext.MilestoneSteps.FindAsync(step2.Id);
        var dbStep3 = await dbContext.MilestoneSteps.FindAsync(step3.Id);

        dbStep3!.OrderIndex.Should().Be(1);
        dbStep1!.OrderIndex.Should().Be(2);
        dbStep2!.OrderIndex.Should().Be(3);
    }

    [Fact]
    public async Task ReorderMilestoneStepsAsync_ByNonExpert_ThrowsUnauthorizedException()
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
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.ReorderMilestoneStepsAsync(clientId, milestoneId, new List<Guid>()));
        ex.Message.Should().Be("Only the expert can manage milestone steps.");
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
            service.UpdateMilestoneStepAsync(expertId, stepId, new Request.UpdateMilestoneStepRequest { Title = "New Title" }));
        updateEx.Message.Should().Be("Cannot modify steps in a completed or cancelled project.");

        // Act & Assert for DeleteMilestoneStepAsync
        var deleteEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeleteMilestoneStepAsync(expertId, stepId));
        deleteEx.Message.Should().Be("Cannot modify steps in a completed or cancelled project.");

        // Act & Assert for ReorderMilestoneStepsAsync
        var reorderEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(expertId, milestoneId, new List<Guid> { stepId }));
        reorderEx.Message.Should().Be("Cannot modify steps in a completed or cancelled project.");

        // Act & Assert for UpdateStepStatusAsync
        var statusEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStepStatusAsync(expertId, stepId, new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.SKIPPED }));
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
            service.UpdateMilestoneStepAsync(expertId, stepId, new Request.UpdateMilestoneStepRequest { Title = "New Title" }));
        updateEx.Message.Should().Be("Cannot modify steps for a finalized milestone.");

        // Act & Assert for DeleteMilestoneStepAsync
        var deleteEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeleteMilestoneStepAsync(expertId, stepId));
        deleteEx.Message.Should().Be("Cannot modify steps for a finalized milestone.");

        // Act & Assert for ReorderMilestoneStepsAsync
        var reorderEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(expertId, milestoneId, new List<Guid> { stepId }));
        reorderEx.Message.Should().Be("Cannot modify steps for a finalized milestone.");

        // Act & Assert for UpdateStepStatusAsync
        var statusEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStepStatusAsync(expertId, stepId, new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.SKIPPED }));
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

        var stepIds = new List<Guid> { step1.Id };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(expertId, milestoneId, stepIds));
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

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddMilestoneStepAsync(expertId, milestoneId, request));
        ex.Message.Should().Be("Cannot add steps to a finalized milestone.");
    }

    [Fact]
    public async Task ReorderMilestoneStepsAsync_WithMismatchedStepIds_ThrowsValidationException()
    {
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

        var stepIds = new List<Guid> { step1.Id, Guid.NewGuid() };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(expertId, milestoneId, stepIds));
        ex.Message.Should().Be("All step IDs must be provided for reordering.");
    }

    [Fact]
    public async Task ReorderMilestoneStepsAsync_WithDuplicateStepIds_ThrowsValidationException()
    {
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

        var stepIds = new List<Guid> { step1.Id, step1.Id };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ReorderMilestoneStepsAsync(expertId, milestoneId, stepIds));
        ex.Message.Should().Be("All step IDs must be provided for reordering.");
    }

    [Theory]
    [InlineData("Created")]
    [InlineData("Funded")]
    [InlineData("Completed")]
    public async Task SystemDefaultSteps_CannotBeModifiedOrDeleted(string systemStepTitle)
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 1", Amount = 100, Status = MilestoneStatus.CREATED };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = systemStepTitle, OrderIndex = 0, Status = MilestoneStepStatus.COMPLETED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = GetService(dbContext);

        // Act & Assert Update
        var updateEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneStepAsync(expertId, stepId, new Request.UpdateMilestoneStepRequest { Title = "Hacked" }));
        updateEx.Message.Should().Be("Cannot modify or delete default system milestone steps.");

        // Act & Assert Delete
        var deleteEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeleteMilestoneStepAsync(expertId, stepId));
        deleteEx.Message.Should().Be("Cannot modify or delete default system milestone steps.");

        // Act & Assert Status Update
        var statusEx = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStepStatusAsync(expertId, stepId, new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.IN_PROGRESS }));
        statusEx.Message.Should().Be("Cannot modify or delete default system milestone steps.");
    }

    [Fact]
    public async Task AddMilestoneStepAsync_TitleTooLong_ThrowsValidationException()
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
        var request = new Request.CreateMilestoneStepRequest { Title = new string('a', 256), OrderIndex = 1 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddMilestoneStepAsync(expertId, milestoneId, request));
        ex.Message.Should().Be("Title must not exceed 255 characters.");
    }

    [Fact]
    public async Task AddMilestoneStepAsync_DescriptionTooLong_ThrowsValidationException()
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
        var request = new Request.CreateMilestoneStepRequest { Title = "Valid title", Description = new string('a', 1001), OrderIndex = 1 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddMilestoneStepAsync(expertId, milestoneId, request));
        ex.Message.Should().Be("Description must not exceed 1000 characters.");
    }

    [Fact]
    public async Task UpdateMilestoneStepAsync_TitleTooLong_ThrowsValidationException()
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
        var request = new Request.UpdateMilestoneStepRequest { Title = new string('a', 256) };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneStepAsync(expertId, stepId, request));
        ex.Message.Should().Be("Title must not exceed 255 characters.");
    }

    [Fact]
    public async Task UpdateStepStatusAsync_BlockReasonTooLong_ThrowsValidationException()
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
        var request = new Request.UpdateStepStatusRequest { Status = MilestoneStepStatus.BLOCKED, Reason = new string('a', 1001) };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStepStatusAsync(expertId, stepId, request));
        ex.Message.Should().Be("Reason must not exceed 1000 characters.");
    }
}
