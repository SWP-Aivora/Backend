using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.DisputeService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class DisputeServiceTests
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
    public async Task OpenDisputeAsync_UpdatesMilestoneAndProjectStatuses()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var clientUser = new User { Id = clientId, FullName = "Client", Role = UserRole.CLIENT, Email = "c@t.com", PasswordHash = "x" };
        var expertUser = new User { Id = expertId, FullName = "Expert", Role = UserRole.EXPERT, Email = "e@t.com", PasswordHash = "x" };
        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Dispute Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 500, Status = MilestoneStatus.FUNDED, Title = "M1" };
        var payment = new Payment { MilestoneId = milestoneId, ProjectId = projectId, PayerId = clientId, PayeeId = expertId, Amount = 500, Status = PaymentStatus.RELEASED };

        dbContext.Users.AddRange(clientUser, expertUser);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var notificationService = new MockNotificationService();
        var service = new Service(dbContext, notificationService, Mock.Of<ILogger<Service>>());
        var request = new Request.OpenDisputeRequest { MilestoneId = milestoneId, Reason = "Poor quality" };

        // Act
        var result = await service.OpenDisputeAsync(clientId, request);

        // Assert
        result.Status.Should().Be(DisputeStatus.OPEN.ToString());

        var updatedMilestone = await dbContext.Milestones.FindAsync(milestoneId);
        updatedMilestone!.Status.Should().Be(MilestoneStatus.DISPUTED);

        var updatedProject = await dbContext.Projects.FindAsync(projectId);
        updatedProject!.Status.Should().Be(ProjectStatus.DISPUTED);

        // Payment remains RELEASED (no frozen logic)
        var updatedPayment = await dbContext.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.RELEASED);
    }

    [Fact]
    public async Task ResolveDisputeAsync_ShouldUpdateStatusAndNote()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var clientUser = new User { Id = clientId, FullName = "Client", Role = UserRole.CLIENT, Email = "c@t.com", PasswordHash = "x" };
        var expertUser = new User { Id = expertId, FullName = "Expert", Role = UserRole.EXPERT, Email = "e@t.com", PasswordHash = "x" };
        var adminUser = new User { Id = adminId, FullName = "Admin", Role = UserRole.ADMIN, Email = "a@t.com", PasswordHash = "x" };

        var project = new Project { Id = Guid.NewGuid(), ClientId = clientId, ExpertId = expertId, Title = "Resolve Project", Status = ProjectStatus.DISPUTED };
        var milestone = new Milestone { Id = Guid.NewGuid(), ProjectId = project.Id, Amount = 500, Status = MilestoneStatus.DISPUTED, Title = "M1" };
        var payment = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = clientId, PayeeId = expertId, Amount = 500, Status = PaymentStatus.RELEASED };

        var dispute = new Dispute { Id = Guid.NewGuid(), ProjectId = project.Id, MilestoneId = milestone.Id, PaymentId = payment.Id, OpenedBy = clientId, AgainstUserId = expertId, Status = DisputeStatus.OPEN, Reason = "X" };

        dbContext.Users.AddRange(clientUser, expertUser, adminUser);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        dbContext.Disputes.Add(dispute);
        await dbContext.SaveChangesAsync();

        var notificationService = new MockNotificationService();
        var service = new Service(dbContext, notificationService, Mock.Of<ILogger<Service>>());
        var resolveRequest = new Request.ResolveDisputeRequest
        {
            ResolutionNote = "Resolved via external mediation"
        };

        // Act
        await service.ResolveDisputeAsync(adminId, dispute.Id, resolveRequest);


        var updatedDispute = await dbContext.Disputes.FindAsync(dispute.Id);
        updatedDispute!.Status.Should().Be(DisputeStatus.RESOLVED);
        updatedDispute!.ResolutionNote.Should().Be("Resolved via external mediation");

        // Assert - milestone unlocked to SUBMITTED
        var updatedMilestone = await dbContext.Milestones.FindAsync(milestone.Id);
        updatedMilestone!.Status.Should().Be(MilestoneStatus.SUBMITTED);

        // Assert - project reverted to ACTIVE
        var updatedProject = await dbContext.Projects.FindAsync(project.Id);
        updatedProject!.Status.Should().Be(ProjectStatus.ACTIVE);
    }

    private class MockNotificationService : Aivora.Services.NotificationService.IService
    {
        public Task<Aivora.Services.NotificationService.Response.NotificationResponse> SendNotificationAsync(Guid userId, string title, string message, string? type = null, string? linkUrl = null)
            => Task.FromResult(new Aivora.Services.NotificationService.Response.NotificationResponse
            {
                Id = Guid.NewGuid(),
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = linkUrl,
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow
            });

        public Task<Aivora.Services.Base.Response.PageResult<Aivora.Services.NotificationService.Response.NotificationResponse>> GetUserNotificationsAsync(Guid userId, Aivora.Services.Base.Request.PageRequest pageRequest)
            => throw new NotImplementedException();

        public Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId)
            => throw new NotImplementedException();

        public Task<bool> MarkAllAsReadAsync(Guid userId)
            => throw new NotImplementedException();

        public Task<int> GetUnreadCountAsync(Guid userId)
            => throw new NotImplementedException();
    }
}
