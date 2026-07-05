using System;
using System.Linq;
using System.Threading.Tasks;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.DisputeService;
using Aivora.Services.Treasury;
using Aivora.Services.Exceptions;
using Aivora.Repositories.Data;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;
using Xunit;
using NotificationIService = Aivora.Services.NotificationService.IService;
using DisputeService = Aivora.Services.DisputeService.Service;

namespace Aivora.Tests.UnitTests
{
    public class DisputeServiceTests : IDisposable
    {
        private readonly AivoraDbContext _dbContext;
        private readonly Aivora.Services.DisputeService.IService _disputeService;
        private readonly ITreasury _treasury;
        private readonly MockNotificationService _mockNotificationService;

        public DisputeServiceTests()
        {
            var options = new DbContextOptionsBuilder<AivoraDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AivoraDbContext(options);

            // Mock services for unit tests
            _treasury = new MockTreasuryService(_dbContext);
            _mockNotificationService = new MockNotificationService();

            _disputeService = new DisputeService(_dbContext, _treasury, _mockNotificationService);
        }

        [Fact]
        public async Task ResolveDispute_ReleaseToExpert_ShouldNotThrowTransactionException()
        {
            // Arrange
            var admin = new User { Id = Guid.NewGuid(), Role = UserRole.ADMIN, Email = "admin@test.com", FullName = "Admin", PasswordHash = "hash" };
            var client = new User { Id = Guid.NewGuid(), Role = UserRole.CLIENT, Email = "client@test.com", FullName = "Client", PasswordHash = "hash" };
            var expert = new User { Id = Guid.NewGuid(), Role = UserRole.EXPERT, Email = "expert@test.com", FullName = "Expert", PasswordHash = "hash" };

            _dbContext.Users.AddRange(admin, client, expert);

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
                Status = MilestoneStatus.DISPUTED
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

            var dispute = new Dispute
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                MilestoneId = milestone.Id,
                PaymentId = payment.Id,
                OpenedBy = client.Id,
                AgainstUserId = expert.Id,
                Reason = "Test dispute reason",
                Status = DisputeStatus.OPEN
            };
            _dbContext.Disputes.Add(dispute);

            await _dbContext.SaveChangesAsync();

            var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
            {
                ResolutionType = DisputeResolutionType.RELEASE_TO_EXPERT,
                ResolutionNote = "Release to expert"
            };

            // Act & Assert
            // This should not throw any exception including nested transaction exception
            var response = await _disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

            Assert.NotNull(response);
            Assert.Equal(DisputeStatus.RESOLVED.ToString(), response.Status);
        }

        // ==================== OpenDispute Tests ====================

        [Fact]
        public async Task OpenDispute_ShouldFreezePayment()
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

            // Assert
            Assert.NotNull(response);
            var updatedPayment = await _dbContext.Payments.FindAsync(payment.Id);
            Assert.Equal(PaymentStatus.RELEASED, updatedPayment!.Status);
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
        public async Task CloseDispute_ByOpener_ShouldSucceed()
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

            var dbDispute = await _dbContext.Disputes.FindAsync(dispute.Id);
            Assert.Equal(DisputeStatus.CLOSED, dbDispute!.Status);

            // Milestone should revert from DISPUTED to IN_PROGRESS
            var dbMilestone = await _dbContext.Milestones.FindAsync(dispute.MilestoneId);
            Assert.NotNull(dbMilestone);
            Assert.Equal(MilestoneStatus.IN_PROGRESS, dbMilestone!.Status);
        }

        [Fact]
        public async Task CloseDispute_WithFrozenPayment_ShouldUnfreezeToHeld()
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
            Assert.Equal(PaymentStatus.RELEASED, updatedPayment!.Status);
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
            Assert.Contains("not authorized", ex.Message.ToLower());
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

            // Assert - dispute status
            Assert.NotNull(response);
            Assert.Equal(DisputeStatus.UNDER_REVIEW.ToString(), response.Status);

            var dbDispute = await _dbContext.Disputes.FindAsync(dispute.Id);
            Assert.Equal(DisputeStatus.UNDER_REVIEW, dbDispute!.Status);

            // Assert - notification was sent to opener
            Assert.True(_mockNotificationService.WasCalled);
            Assert.Equal(client.Id, _mockNotificationService.LastUserId);
            Assert.Equal("Additional evidence requested", _mockNotificationService.LastTitle);
            Assert.Equal("Please provide additional screenshots", _mockNotificationService.LastMessage);
            Assert.Equal("DISPUTE", _mockNotificationService.LastType);
            Assert.Contains(dispute.Id.ToString(), _mockNotificationService.LastLinkUrl);
        }

        [Fact]
        public async Task RequestEvidence_AlreadyUnderReview_ShouldStayUnderReview()
        {
            // Arrange
            var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.UNDER_REVIEW);

            var request = new Aivora.Services.DisputeService.Request.RequestEvidenceRequest
            {
                Note = "Additional evidence 2"
            };

            // Act
            var response = await _disputeService.RequestEvidenceAsync(admin.Id, dispute.Id, request);

            // Assert - stays UNDER_REVIEW
            Assert.Equal(DisputeStatus.UNDER_REVIEW.ToString(), response.Status);
            Assert.True(_mockNotificationService.WasCalled);
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

        [Fact]
        public async Task RequestEvidence_AlreadyClosed_ShouldThrowValidation()
        {
            // Arrange
            var admin = await SeedUserAsync(UserRole.ADMIN, "admin@test.com");
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.CLOSED);

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
        public async Task DeleteEvidence_ByNonOpener_ShouldThrowUnauthorized()
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
                Content = "Test evidence"
            };
            _dbContext.DisputeEvidences.Add(evidence);
            await _dbContext.SaveChangesAsync();

            // Act & Assert - expert (not the opener) tries to delete
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(
                () => _disputeService.DeleteEvidenceAsync(expert.Id, dispute.Id, evidence.Id));
            Assert.Contains("not authorized", ex.Message.ToLower());
        }

        [Fact]
        public async Task DeleteEvidence_DisputeResolved_ShouldThrowValidation()
        {
            // Arrange
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.RESOLVED);

            var evidence = new DisputeEvidence
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute.Id,
                SubmittedBy = client.Id,
                Content = "Test evidence"
            };
            _dbContext.DisputeEvidences.Add(evidence);
            await _dbContext.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => _disputeService.DeleteEvidenceAsync(client.Id, dispute.Id, evidence.Id));
            Assert.Contains("closed", ex.Message.ToLower());
        }

        [Fact]
        public async Task DeleteEvidence_DisputeClosed_ShouldThrowValidation()
        {
            // Arrange
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.CLOSED);

            var evidence = new DisputeEvidence
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute.Id,
                SubmittedBy = client.Id,
                Content = "Test evidence"
            };
            _dbContext.DisputeEvidences.Add(evidence);
            await _dbContext.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(
                () => _disputeService.DeleteEvidenceAsync(client.Id, dispute.Id, evidence.Id));
            Assert.Contains("closed", ex.Message.ToLower());
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

        [Fact]
        public async Task DeleteEvidence_WrongDispute_ShouldThrowNotFound()
        {
            // Arrange
            var client = await SeedUserAsync(UserRole.CLIENT, "client@test.com");
            var expert = await SeedUserAsync(UserRole.EXPERT, "expert@test.com");
            var dispute1 = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);
            var dispute2 = await SeedDisputeAsync(client.Id, expert.Id, DisputeStatus.OPEN);

            var evidence = new DisputeEvidence
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute1.Id,
                SubmittedBy = client.Id,
                Content = "Test evidence"
            };
            _dbContext.DisputeEvidences.Add(evidence);
            await _dbContext.SaveChangesAsync();

            // Act & Assert - passing wrong disputeId
            await Assert.ThrowsAsync<NotFoundException>(
                () => _disputeService.DeleteEvidenceAsync(client.Id, dispute2.Id, evidence.Id));
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

        private class MockTreasuryService : ITreasury
        {
            private readonly AivoraDbContext _dbContext;

            public MockTreasuryService(AivoraDbContext dbContext)
            {
                _dbContext = dbContext;
            }

            public async Task FundMilestoneAsync(Guid clientId, Guid milestoneId)
            {
                // Mock implementation
                await Task.CompletedTask;
            }

            public async Task ReleaseMilestoneAsync(Guid clientId, Guid milestoneId)
            {
                // Mock implementation - simulates release without nested transaction
                var milestone = await _dbContext.Milestones.FindAsync(milestoneId);
                if (milestone != null)
                {
                    milestone.Status = MilestoneStatus.RELEASED;
                    await _dbContext.SaveChangesAsync();
                }
            }

            public Task PayDepositAsync(Guid clientId, Guid milestoneId) => Task.CompletedTask;
            public Task PayRemainingAsync(Guid clientId, Guid milestoneId) => Task.CompletedTask;

            public async Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, string reason)
            {
                // Mock implementation - simulates refund without nested transaction
                var milestone = await _dbContext.Milestones.FindAsync(milestoneId);
                if (milestone != null)
                {
                    milestone.Status = MilestoneStatus.REFUNDED;
                    await _dbContext.SaveChangesAsync();
                }
            }

            public async Task SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason)
            {
                // Mock implementation - simulates split without nested transaction
                var milestone = await _dbContext.Milestones.FindAsync(milestoneId);
                if (milestone != null)
                {
                    milestone.Status = MilestoneStatus.RELEASED;
                    await _dbContext.SaveChangesAsync();
                }
            }

            public async Task FreezeFundsAsync(Guid milestoneId, string reason)
            {
                var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
                if (payment != null && payment.Status == PaymentStatus.HELD)
                {
                    payment.Status = PaymentStatus.FROZEN;
                    await _dbContext.SaveChangesAsync();
                }
            }

            public async Task UnfreezeFundsAsync(Guid milestoneId, string reason)
            {
                var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
                if (payment != null && payment.Status == PaymentStatus.FROZEN)
                {
                    payment.Status = PaymentStatus.HELD;
                    await _dbContext.SaveChangesAsync();
                }
            }

            public async Task SyncProjectStatusAsync(Guid projectId)
            {
                // Mock implementation
                await Task.CompletedTask;
            }

            public async Task MarkProjectDisputedAsync(Guid projectId)
            {
                // Mock implementation
                await Task.CompletedTask;
            }
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