using System;
using System.Threading.Tasks;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.DisputeService;
using Aivora.Services.Treasury;
using Aivora.Services.Exceptions;
using Aivora.Repositories.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests.UnitTests
{
    public class DisputeServiceTests : IDisposable
    {
        private readonly AivoraDbContext _dbContext;
        private readonly IService _disputeService;
        private readonly ITreasury _treasury;

        public DisputeServiceTests()
        {
            var options = new DbContextOptionsBuilder<AivoraDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AivoraDbContext(options);

            // Mock Treasury service for unit tests
            _treasury = new MockTreasuryService(_dbContext);

            _disputeService = new Service(_dbContext, _treasury);
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

            public async Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, decimal amount, string reason)
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
                // Mock implementation
                await Task.CompletedTask;
            }

            public async Task UnfreezeFundsAsync(Guid milestoneId, string reason)
            {
                // Mock implementation
                await Task.CompletedTask;
            }

            public async Task RequestRevisionAsync(Guid milestoneId, string reason)
            {
                // Mock implementation - simulates request revision without payment status conflict
                var milestone = await _dbContext.Milestones.FindAsync(milestoneId);
                if (milestone != null)
                {
                    milestone.Status = MilestoneStatus.REVISION_REQUESTED;
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
    }
}