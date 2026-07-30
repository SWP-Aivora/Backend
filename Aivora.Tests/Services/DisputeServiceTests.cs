using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.DisputeService;
using Aivora.Services.Exceptions;
using Aivora.Repositories.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using DisputeService = Aivora.Services.DisputeService.Service;

namespace Aivora.Tests.Services;

public class DisputeServiceTests : IDisposable
{
    private readonly AivoraDbContext _dbContext;
    private readonly Aivora.Services.DisputeService.IService _disputeService;
    private readonly MockNotificationService _mockNotificationService;

    public DisputeServiceTests()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AivoraDbContext(options);
        _mockNotificationService = new MockNotificationService();

        _disputeService = new DisputeService(_dbContext, _mockNotificationService, Mock.Of<ILogger<DisputeService>>(), new Aivora.Services.RealtimeService.NullRealtimeService());
    }

    // ==================== ResolveDispute Tests ====================

    [Fact]
    public async Task ResolveDispute_ShouldOnlyUpdateStatusAndNote()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
        {
            ResolutionNote = "Resolved via external mediation"
        };

        // Act
        var response = await _disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(DisputeStatus.RESOLVED.ToString());
        response.ResolutionNote.Should().Be("Resolved via external mediation");
        response.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveDisputeAsync_WhenAlreadyClosed_Throws()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin2@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client2@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert2@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.CLOSED);

        var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
        {
            ResolutionNote = "Too late"
        };

        // Act
        var act = () => _disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ResolveDispute_NoDeliverable_ShouldRevertToInProgress()
    {
        // Arrange - dispute opened before Expert ever submitted a deliverable (SubmittedAt = null)
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
        {
            ResolutionNote = "Mediation complete"
        };

        // Act
        await _disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

        // Assert - milestone must NOT unlock Approve & Pay when no deliverable was ever submitted
        var dbMilestone = await _dbContext.Milestones.FindAsync(dispute.MilestoneId);
        dbMilestone.Should().NotBeNull();
        dbMilestone!.Status.Should().Be(MilestoneStatus.IN_PROGRESS);
    }

    [Fact]
    public async Task ResolveDispute_WithDeliverable_ShouldRestoreSubmitted()
    {
        // Arrange - dispute opened after Expert already submitted a deliverable (SubmittedAt set)
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var milestone = await _dbContext.Milestones.FindAsync(dispute.MilestoneId);
        milestone!.SubmittedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
        {
            ResolutionNote = "Mediation complete"
        };

        // Act
        await _disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

        // Assert - deliverable exists, safe to unlock Approve & Pay
        var dbMilestone = await _dbContext.Milestones.FindAsync(dispute.MilestoneId);
        dbMilestone.Should().NotBeNull();
        dbMilestone!.Status.Should().Be(MilestoneStatus.SUBMITTED);
    }

    [Fact]
    public async Task ResolveDispute_ShouldSyncProjectStatus()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
        {
            ResolutionNote = "External arbitration"
        };

        // Act
        await _disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

        // Assert - project should revert from DISPUTED to ACTIVE
        var dbProject = await _dbContext.Projects.FindAsync(dispute.ProjectId);
        dbProject.Should().NotBeNull();
        dbProject!.Status.Should().Be(ProjectStatus.ACTIVE);
    }

    [Fact]
    public async Task ResolveDispute_AlreadyResolved_ShouldThrowValidation()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.RESOLVED);

        var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
        {
            ResolutionNote = "Try again"
        };

        // Act & Assert
        var act = () => _disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.ToLower().Should().Contain("already");
    }

    // ==================== OpenDispute Tests ====================

    [Fact]
    public async Task OpenDispute_ShouldGateMilestone()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            ExpertId = expert.Id,
            Status = ProjectStatus.ACTIVE,
            Title = "Test Project"
        };
        _dbContext.Projects.Add(project);

        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Test Milestone",
            Amount = 1000,
            Status = MilestoneStatus.IN_PROGRESS
        };
        _dbContext.Milestones.Add(milestone);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MilestoneId = milestone.Id,
            ProjectId = project.Id,
            PayerId = client.Id,
            PayeeId = expert.Id,
            Amount = 1000,
            Status = PaymentStatus.RELEASED
        };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var request = new Aivora.Services.DisputeService.Request.OpenDisputeRequest
        {
            MilestoneId = milestone.Id,
            Reason = "Test dispute reason"
        };

        // Act
        var response = await _disputeService.OpenDisputeAsync(client.Id, request);

        // Assert - milestone should be DISPUTED (gated)
        response.Should().NotBeNull();
        var updatedMilestone = await _dbContext.Milestones.FindAsync(milestone.Id);
        updatedMilestone!.Status.Should().Be(MilestoneStatus.DISPUTED);

        // Assert - project should be DISPUTED
        var updatedProject = await _dbContext.Projects.FindAsync(project.Id);
        updatedProject!.Status.Should().Be(ProjectStatus.DISPUTED);

        // Assert - payment should NOT be frozen (no Treasury.FreezeFunds call)
        var updatedPayment = await _dbContext.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.RELEASED);
    }

    [Fact]
    public async Task OpenDispute_WithClosedDisputeAndUnderQuota_ShouldSucceed()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.CLOSED);

        var payment = await _dbContext.Payments.FindAsync(dispute.PaymentId);
        payment!.Status = PaymentStatus.RELEASED;
        await _dbContext.SaveChangesAsync();

        var request = new Aivora.Services.DisputeService.Request.OpenDisputeRequest
        {
            MilestoneId = dispute.MilestoneId,
            Reason = "Second dispute"
        };

        // Act
        var response = await _disputeService.OpenDisputeAsync(client.Id, request);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(DisputeStatus.OPEN.ToString());
    }

    [Fact]
    public async Task OpenDispute_WithActiveDispute_ShouldThrowValidation()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var request = new Aivora.Services.DisputeService.Request.OpenDisputeRequest
        {
            MilestoneId = dispute.MilestoneId,
            Reason = "Second dispute while first is active"
        };

        // Act & Assert
        var act = () => _disputeService.OpenDisputeAsync(client.Id, request);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.Should().Contain("active dispute");
    }

    [Fact]
    public async Task OpenDispute_ExceedingQuota_ShouldThrowValidation()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        
        var dispute1 = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.CLOSED);
        
        var dispute2 = new Dispute { Id = Guid.NewGuid(), ProjectId = dispute1.ProjectId, MilestoneId = dispute1.MilestoneId, PaymentId = dispute1.PaymentId, OpenedBy = client.Id, AgainstUserId = expert.Id, Reason = "2", Status = DisputeStatus.CLOSED };
        _dbContext.Disputes.Add(dispute2);
        
        var dispute3 = new Dispute { Id = Guid.NewGuid(), ProjectId = dispute1.ProjectId, MilestoneId = dispute1.MilestoneId, PaymentId = dispute1.PaymentId, OpenedBy = client.Id, AgainstUserId = expert.Id, Reason = "3", Status = DisputeStatus.CLOSED };
        _dbContext.Disputes.Add(dispute3);
        
        var payment = await _dbContext.Payments.FindAsync(dispute1.PaymentId);
        payment!.Status = PaymentStatus.RELEASED;
        await _dbContext.SaveChangesAsync();

        var request = new Aivora.Services.DisputeService.Request.OpenDisputeRequest
        {
            MilestoneId = dispute1.MilestoneId,
            Reason = "Fourth dispute"
        };

        // Act & Assert
        var act = () => _disputeService.OpenDisputeAsync(client.Id, request);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.Should().Contain("limit reached");
    }

    // ==================== CloseDispute Tests ====================

    [Fact]
    public async Task CloseDispute_ByOpener_ShouldUnlockMilestone()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        // Act
        var response = await _disputeService.CloseDisputeAsync(client.Id, dispute.Id);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(DisputeStatus.CLOSED.ToString());

        // Milestone should revert from DISPUTED to IN_PROGRESS
        var dbMilestone = await _dbContext.Milestones.FindAsync(dispute.MilestoneId);
        dbMilestone.Should().NotBeNull();
        dbMilestone!.Status.Should().Be(MilestoneStatus.IN_PROGRESS);

        // Project should revert from DISPUTED to ACTIVE
        var dbProject = await _dbContext.Projects.FindAsync(dispute.ProjectId);
        dbProject.Should().NotBeNull();
        dbProject!.Status.Should().Be(ProjectStatus.ACTIVE);
    }

    [Fact]
    public async Task CloseDispute_ShouldNotAffectPaymentStatus()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        // Simulate payment was RELEASED (deposit)
        var payment = await _dbContext.Payments.FindAsync(dispute.PaymentId);
        payment!.Status = PaymentStatus.RELEASED;
        await _dbContext.SaveChangesAsync();

        // Act
        await _disputeService.CloseDisputeAsync(client.Id, dispute.Id);

        // Assert
        var updatedPayment = await _dbContext.Payments.FindAsync(dispute.PaymentId);
        updatedPayment!.Status.Should().Be(PaymentStatus.RELEASED);
    }

    [Fact]
    public async Task CloseDispute_ByNonOpener_ShouldThrowForbidden()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        // Act & Assert - expert (not the opener) tries to close
        var act = () => _disputeService.CloseDisputeAsync(expert.Id, dispute.Id);
        var ex = await act.Should().ThrowAsync<ForbiddenException>();
        ex.Which.Message.ToLower().Should().Contain("only the user who opened the dispute can close it");
    }

    [Fact]
    public async Task CloseDispute_AlreadyResolved_ShouldThrowValidation()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.RESOLVED);

        // Act & Assert
        var act = () => _disputeService.CloseDisputeAsync(client.Id, dispute.Id);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.ToLower().Should().Contain("already");
    }

    [Fact]
    public async Task CloseDispute_AlreadyClosed_ShouldThrowValidation()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.CLOSED);

        // Act & Assert
        var act = () => _disputeService.CloseDisputeAsync(client.Id, dispute.Id);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.ToLower().Should().Contain("already");
    }

    // ==================== RequestEvidence Tests ====================

    [Fact]
    public async Task RequestEvidence_ByAdmin_ShouldSetUnderReviewAndSendNotification()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var request = new Aivora.Services.DisputeService.Request.RequestEvidenceRequest
        {
            Note = "Please provide additional screenshots"
        };

        // Act
        var response = await _disputeService.RequestEvidenceAsync(admin.Id, dispute.Id, request);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(DisputeStatus.UNDER_REVIEW.ToString());
        _mockNotificationService.WasCalled.Should().BeTrue();
        // It sends to both, the last one is againstUserId
        _mockNotificationService.LastUserId.Should().Be(expert.Id);
    }

    [Fact]
    public async Task RequestEvidence_AlreadyResolved_ShouldThrowValidation()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.RESOLVED);

        var request = new Aivora.Services.DisputeService.Request.RequestEvidenceRequest
        {
            Note = "Additional evidence"
        };

        // Act & Assert
        var act = () => _disputeService.RequestEvidenceAsync(admin.Id, dispute.Id, request);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.ToLower().Should().Contain("already");
    }

    // ==================== DeleteEvidence Tests ====================

    [Fact]
    public async Task DeleteEvidence_ByOpener_ShouldSucceed()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var evidence = new DisputeEvidence
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            SubmittedBy = client.Id,
            Content = "Test evidence content"
        };
        _dbContext.DisputeEvidences.Add(evidence);
        await _dbContext.SaveChangesAsync();

        // Act
        await _disputeService.DeleteEvidenceAsync(client.Id, dispute.Id, evidence.Id);

        // Assert
        var dbEvidence = await _dbContext.DisputeEvidences.FindAsync(evidence.Id);
        dbEvidence.Should().BeNull();
    }

    [Fact]
    public async Task DeleteEvidence_NotFound_ShouldThrowNotFound()
    {
        // Arrange
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        // Act & Assert
        var act = () => _disputeService.DeleteEvidenceAsync(client.Id, dispute.Id, Guid.NewGuid());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ==================== Realtime broadcast Tests (#199) ====================

    [Fact]
    public async Task OpenDispute_ShouldBroadcastDisputeUpdatedExactlyOnce()
    {
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");

        var project = new Project { Id = Guid.NewGuid(), ClientId = client.Id, ExpertId = expert.Id, Status = ProjectStatus.ACTIVE, Title = "Test Project" };
        _dbContext.Projects.Add(project);
        var milestone = new Milestone { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "Test Milestone", Amount = 1000, Status = MilestoneStatus.IN_PROGRESS };
        _dbContext.Milestones.Add(milestone);
        var payment = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = client.Id, PayeeId = expert.Id, Amount = 1000, Status = PaymentStatus.RELEASED };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var mockRealtime = new Mock<Aivora.Services.RealtimeService.IService>();
        var service = new DisputeService(_dbContext, _mockNotificationService, Mock.Of<ILogger<DisputeService>>(), mockRealtime.Object);

        var request = new Aivora.Services.DisputeService.Request.OpenDisputeRequest { MilestoneId = milestone.Id, Reason = "Test dispute reason" };
        await service.OpenDisputeAsync(client.Id, request);

        mockRealtime.Verify(r => r.SendDisputeUpdatedAsync(project.Id, It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task ResolveDispute_ShouldBroadcastDisputeUpdatedExactlyOnce()
    {
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var mockRealtime = new Mock<Aivora.Services.RealtimeService.IService>();
        var service = new DisputeService(_dbContext, _mockNotificationService, Mock.Of<ILogger<DisputeService>>(), mockRealtime.Object);

        var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest { ResolutionNote = "Resolved" };
        await service.ResolveDisputeAsync(admin.Id, dispute.Id, request);

        mockRealtime.Verify(r => r.SendDisputeUpdatedAsync(dispute.ProjectId, dispute.Id), Times.Once);
    }

    [Fact]
    public async Task CloseDispute_ShouldBroadcastDisputeUpdatedExactlyOnce()
    {
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var mockRealtime = new Mock<Aivora.Services.RealtimeService.IService>();
        var service = new DisputeService(_dbContext, _mockNotificationService, Mock.Of<ILogger<DisputeService>>(), mockRealtime.Object);

        await service.CloseDisputeAsync(client.Id, dispute.Id);

        mockRealtime.Verify(r => r.SendDisputeUpdatedAsync(dispute.ProjectId, dispute.Id), Times.Once);
    }

    // ==================== "CANCELLED must be terminal" Tests ====================

    [Fact]
    public async Task OpenDispute_OnCancelledProject_ShouldThrowValidation()
    {
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");

        var project = new Project { Id = Guid.NewGuid(), ClientId = client.Id, ExpertId = expert.Id, Status = ProjectStatus.CANCELLED, Title = "Cancelled Project" };
        _dbContext.Projects.Add(project);
        var milestone = new Milestone { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "Test Milestone", Amount = 1000, Status = MilestoneStatus.RELEASED };
        _dbContext.Milestones.Add(milestone);
        var payment = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = client.Id, PayeeId = expert.Id, Amount = 1000, Status = PaymentStatus.RELEASED };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var request = new Aivora.Services.DisputeService.Request.OpenDisputeRequest { MilestoneId = milestone.Id, Reason = "Too late" };

        var act = () => _disputeService.OpenDisputeAsync(client.Id, request);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.ToLower().Should().Contain("closed project");
    }

    [Fact]
    public async Task ResolveDispute_OnCancelledProject_ShouldNotFlipProjectStatusAway()
    {
        // Arrange - dispute still OPEN/resolvable but project was cancelled out-of-band (e.g. via #202 cancel-disputed)
        var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
        var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
        var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
        var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

        var project = await _dbContext.Projects.FindAsync(dispute.ProjectId);
        project!.Status = ProjectStatus.CANCELLED;
        await _dbContext.SaveChangesAsync();

        var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest { ResolutionNote = "Resolved after cancel" };
        await _disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

        var dbProject = await _dbContext.Projects.FindAsync(dispute.ProjectId);
        dbProject!.Status.Should().Be(ProjectStatus.CANCELLED);
    }

    // ==================== Helpers ====================

    private async Task<User> SeedUserAsync(UserRole role, string email)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Role = role,
            Email = email,
            FullName = $"{role} User",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task<Dispute> SeedDisputeAsync(Guid openedBy, Guid againstUserId, DisputeStatus status)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            ClientId = openedBy,
            ExpertId = againstUserId,
            Status = status == DisputeStatus.RESOLVED ? ProjectStatus.COMPLETED : ProjectStatus.ACTIVE,
            Title = "Test Project"
        };
        _dbContext.Projects.Add(project);

        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Test Milestone",
            Amount = 1000,
            Status = status == DisputeStatus.RESOLVED ? MilestoneStatus.RELEASED : MilestoneStatus.DISPUTED
        };
        _dbContext.Milestones.Add(milestone);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MilestoneId = milestone.Id,
            ProjectId = project.Id,
            PayerId = openedBy,
            PayeeId = againstUserId,
            Amount = 1000,
            Status = PaymentStatus.RELEASED
        };
        _dbContext.Payments.Add(payment);

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            MilestoneId = milestone.Id,
            PaymentId = payment.Id,
            OpenedBy = openedBy,
            AgainstUserId = againstUserId,
            Reason = "Test reason",
            Status = status
        };
        _dbContext.Disputes.Add(dispute);
        await _dbContext.SaveChangesAsync();
        return dispute;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private class MockNotificationService : Aivora.Services.NotificationService.IService
    {
        public bool WasCalled { get; private set; }
        public Guid LastUserId { get; private set; }
        public string? LastTitle { get; private set; }
        public string? LastMessage { get; private set; }
        public string? LastType { get; private set; }
        public string? LastLinkUrl { get; private set; }

        public Task<Aivora.Services.NotificationService.Response.NotificationResponse> SendNotificationAsync(Guid userId, string title, string message, string? type = null, string? linkUrl = null)
        {
            WasCalled = true;
            LastUserId = userId;
            LastTitle = title;
            LastMessage = message;
            LastType = type;
            LastLinkUrl = linkUrl;
            return Task.FromResult(new Aivora.Services.NotificationService.Response.NotificationResponse
            {
                Id = Guid.NewGuid(),
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = linkUrl,
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<Aivora.Services.Base.Response.PageResult<Aivora.Services.NotificationService.Response.NotificationResponse>> GetUserNotificationsAsync(Guid userId, Aivora.Services.Base.Request.PageRequest pageRequest)
            => throw new NotImplementedException();

        public Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId)
            => throw new NotImplementedException();

        public Task<bool> MarkAllAsReadAsync(Guid userId)
            => throw new NotImplementedException();

        public Task<int> GetUnreadCountAsync(Guid userId)
            => throw new NotImplementedException();

        public void SendInBackground(Guid userId, string title, string message, string? type, string? linkUrl)
        {
        }
    }
}
