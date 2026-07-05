using System;
using System.Linq;
using System.Threading.Tasks;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.DisputeService;
using Aivora.Services.Exceptions;
using Aivora.Repositories.Data;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using DisputeService = Aivora.Services.DisputeService.Service;

namespace Aivora.Tests.UnitTests
{
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

            _disputeService = new DisputeService(_dbContext, _mockNotificationService, Mock.Of<ILogger<DisputeService>>());
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
            Assert.NotNull(response);
            Assert.Equal(DisputeStatus.RESOLVED.ToString(), response.Status);
            Assert.Equal("Resolved via external mediation", response.ResolutionNote);
            Assert.NotNull(response.ResolvedAt);
        }

        [Fact]
        public async Task ResolveDispute_ShouldAutoUnlockMilestone()
        {
            // Arrange
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

            // Assert - milestone should be unlocked to SUBMITTED
            var dbMilestone = await _dbContext.Milestones.FindAsync(dispute.MilestoneId);
            Assert.NotNull(dbMilestone);
            Assert.Equal(MilestoneStatus.SUBMITTED, dbMilestone!.Status);
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
            Assert.NotNull(dbProject);
            Assert.Equal(ProjectStatus.ACTIVE, dbProject!.Status);
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
            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => _disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request));
            Assert.Contains("already", ex.Message.ToLower());
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
                Status = MilestoneStatus.FUNDED
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
                Status = PaymentStatus.HELD
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
            Assert.NotNull(response);
            var updatedMilestone = await _dbContext.Milestones.FindAsync(milestone.Id);
            Assert.Equal(MilestoneStatus.DISPUTED, updatedMilestone!.Status);

            // Assert - project should be DISPUTED
            var updatedProject = await _dbContext.Projects.FindAsync(project.Id);
            Assert.Equal(ProjectStatus.DISPUTED, updatedProject!.Status);

            // Assert - payment should NOT be frozen (no Treasury.FreezeFunds call)
            var updatedPayment = await _dbContext.Payments.FindAsync(payment.Id);
            Assert.Equal(PaymentStatus.HELD, updatedPayment!.Status);
        }

        [Fact]
        public async Task OpenDispute_WithClosedDispute_ShouldThrowValidation()
        {
            // Arrange
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.CLOSED);

            var request = new Aivora.Services.DisputeService.Request.OpenDisputeRequest
            {
                MilestoneId = dispute.MilestoneId,
                Reason = "Second dispute"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => _disputeService.OpenDisputeAsync(client.Id, request));
            Assert.Contains("already closed", ex.Message.ToLower());
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
            Assert.NotNull(response);
            Assert.Equal(DisputeStatus.CLOSED.ToString(), response.Status);

            // Milestone should revert from DISPUTED to IN_PROGRESS
            var dbMilestone = await _dbContext.Milestones.FindAsync(dispute.MilestoneId);
            Assert.NotNull(dbMilestone);
            Assert.Equal(MilestoneStatus.IN_PROGRESS, dbMilestone!.Status);

            // Project should revert from DISPUTED to ACTIVE
            var dbProject = await _dbContext.Projects.FindAsync(dispute.ProjectId);
            Assert.NotNull(dbProject);
            Assert.Equal(ProjectStatus.ACTIVE, dbProject!.Status);
        }

        [Fact]
        public async Task CloseDispute_ByNonOpener_ShouldThrowUnauthorized()
        {
            // Arrange
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

            // Act & Assert - expert (not the opener) tries to close
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(
                () => _disputeService.CloseDisputeAsync(expert.Id, dispute.Id));
            Assert.Contains("only the user who opened the dispute can close it", ex.Message.ToLower());
        }

        [Fact]
        public async Task CloseDispute_AlreadyResolved_ShouldThrowValidation()
        {
            // Arrange
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.RESOLVED);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => _disputeService.CloseDisputeAsync(client.Id, dispute.Id));
            Assert.Contains("already", ex.Message.ToLower());
        }

        [Fact]
        public async Task CloseDispute_AlreadyClosed_ShouldThrowValidation()
        {
            // Arrange
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.CLOSED);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => _disputeService.CloseDisputeAsync(client.Id, dispute.Id));
            Assert.Contains("already", ex.Message.ToLower());
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
            Assert.NotNull(response);
            Assert.Equal(DisputeStatus.UNDER_REVIEW.ToString(), response.Status);
            Assert.True(_mockNotificationService.WasCalled);
            // It sends to both, the last one is againstUserId
            Assert.Equal(expert.Id, _mockNotificationService.LastUserId);
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
            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => _disputeService.RequestEvidenceAsync(admin.Id, dispute.Id, request));
            Assert.Contains("already", ex.Message.ToLower());
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
            Assert.Null(dbEvidence);
        }

        [Fact]
        public async Task DeleteEvidence_NotFound_ShouldThrowNotFound()
        {
            // Arrange
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(
                () => _disputeService.DeleteEvidenceAsync(client.Id, dispute.Id, Guid.NewGuid()));
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
                Status = PaymentStatus.HELD
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
        }
    }
}