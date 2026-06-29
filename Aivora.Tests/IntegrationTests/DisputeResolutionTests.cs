using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Aivora.api;
using Aivora.Repositories.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.DisputeService;
using Aivora.Services.Treasury;

namespace Aivora.Tests.IntegrationTests
{
    public class DisputeResolutionTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public DisputeResolutionTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ResolveDispute_ReleaseToExpert_ShouldNotThrowNestedTransactionException()
        {
            // Arrange - Create test data
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();
            var serviceProvider = scope.ServiceProvider;

            // Create users
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = $"admin-{Guid.NewGuid()}@test.com",
                FullName = "Admin User",
                Role = UserRole.ADMIN,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(admin);

            var client = new User
            {
                Id = Guid.NewGuid(),
                Email = $"client-{Guid.NewGuid()}@test.com",
                FullName = "Client User",
                Role = UserRole.CLIENT,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(client);

            var expert = new User
            {
                Id = Guid.NewGuid(),
                Email = $"expert-{Guid.NewGuid()}@test.com",
                FullName = "Expert User",
                Role = UserRole.EXPERT,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(expert);

            // Create wallets (required by Treasury.ReleaseMilestoneAsync)
            var clientWallet = new Wallet { Id = Guid.NewGuid(), UserId = client.Id, AvailableBalance = 5000, HeldBalance = 1000 };
            var expertWallet = new Wallet { Id = Guid.NewGuid(), UserId = expert.Id, AvailableBalance = 0 };
            dbContext.Wallets.Add(clientWallet);
            dbContext.Wallets.Add(expertWallet);

            // Create JobPost (required FK for Project)
            var jobPost = new JobPost
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                Title = "Test Job",
                OriginalDescription = "Test",
                Status = JobStatus.IN_PROGRESS
            };
            dbContext.JobPosts.Add(jobPost);

            // Create Proposal (required FK for Project)
            var proposal = new Proposal
            {
                Id = Guid.NewGuid(),
                JobId = jobPost.Id,
                ExpertId = expert.Id,
                CoverLetter = "Test proposal",
                ProposedBudget = 1000,
                Status = ProposalStatus.ACCEPTED
            };
            dbContext.Proposals.Add(proposal);

            // Create project
            var project = new Project
            {
                Id = Guid.NewGuid(),
                JobId = jobPost.Id,
                AcceptedProposalId = proposal.Id,
                Title = "Test Project",
                Description = "Test Description",
                ClientId = client.Id,
                ExpertId = expert.Id,
                Status = ProjectStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Projects.Add(project);

            // Create milestone
            var milestone = new Milestone
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Title = "Test Milestone",
                Amount = 1000,
                Status = MilestoneStatus.DISPUTED,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Milestones.Add(milestone);

            // Create payment with HELD status
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                MilestoneId = milestone.Id,
                ProjectId = project.Id,
                PayerId = client.Id,
                PayeeId = expert.Id,
                Amount = 1000,
                Status = PaymentStatus.HELD,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Payments.Add(payment);

            // Create dispute
            var dispute = new Dispute
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                MilestoneId = milestone.Id,
                PaymentId = payment.Id,
                OpenedBy = client.Id,
                AgainstUserId = expert.Id,
                Reason = "Test dispute",
                Status = DisputeStatus.OPEN,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Disputes.Add(dispute);

            await dbContext.SaveChangesAsync();

            // Get services
            var disputeService = serviceProvider.GetRequiredService<IService>();
            var treasuryService = serviceProvider.GetRequiredService<ITreasury>();

            // Act - Resolve dispute with RELEASE_TO_EXPERT
            // This should not throw nested transaction exception
            var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
            {
                ResolutionType = DisputeResolutionType.RELEASE_TO_EXPERT,
                ResolutionNote = "Release funds to expert"
            };

            // Act
            var response = await disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

            // Assert - Should succeed
            Assert.NotNull(response);
            Assert.Equal(DisputeStatus.RESOLVED.ToString(), response.Status);

            // Verify milestone is now RELEASED
            var updatedMilestone = await dbContext.Milestones.FindAsync(milestone.Id);
            Assert.NotNull(updatedMilestone);
            Assert.Equal(MilestoneStatus.RELEASED, updatedMilestone.Status);

            // Verify payment is now RELEASED
            var updatedPayment = await dbContext.Payments.FindAsync(payment.Id);
            Assert.NotNull(updatedPayment);
            Assert.Equal(PaymentStatus.RELEASED, updatedPayment.Status);
        }

        [Fact]
        public async Task ResolveDispute_RequestRevision_ShouldMaintainPaymentStatusAsHeld()
        {
            // Arrange - Create test data
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();
            var serviceProvider = scope.ServiceProvider;

            // Create users
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = $"admin-{Guid.NewGuid()}@test.com",
                FullName = "Admin User",
                Role = UserRole.ADMIN,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(admin);

            var client = new User
            {
                Id = Guid.NewGuid(),
                Email = $"client-{Guid.NewGuid()}@test.com",
                FullName = "Client User",
                Role = UserRole.CLIENT,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(client);

            var expert = new User
            {
                Id = Guid.NewGuid(),
                Email = $"expert-{Guid.NewGuid()}@test.com",
                FullName = "Expert User",
                Role = UserRole.EXPERT,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(expert);

            // Create wallets (required by Treasury.RequestRevisionAsync)
            var clientWallet2 = new Wallet { Id = Guid.NewGuid(), UserId = client.Id, AvailableBalance = 5000, HeldBalance = 1000 };
            var expertWallet2 = new Wallet { Id = Guid.NewGuid(), UserId = expert.Id, AvailableBalance = 0 };
            dbContext.Wallets.Add(clientWallet2);
            dbContext.Wallets.Add(expertWallet2);

            // Create JobPost (required FK for Project)
            var jobPost2 = new JobPost
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                Title = "Test Job 2",
                OriginalDescription = "Test",
                Status = JobStatus.IN_PROGRESS
            };
            dbContext.JobPosts.Add(jobPost2);

            // Create Proposal (required FK for Project)
            var proposal2 = new Proposal
            {
                Id = Guid.NewGuid(),
                JobId = jobPost2.Id,
                ExpertId = expert.Id,
                CoverLetter = "Test proposal",
                ProposedBudget = 1000,
                Status = ProposalStatus.ACCEPTED
            };
            dbContext.Proposals.Add(proposal2);

            // Create project
            var project = new Project
            {
                Id = Guid.NewGuid(),
                JobId = jobPost2.Id,
                AcceptedProposalId = proposal2.Id,
                Title = "Test Project",
                Description = "Test Description",
                ClientId = client.Id,
                ExpertId = expert.Id,
                Status = ProjectStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Projects.Add(project);

            // Create milestone with DISPUTED status
            var milestone = new Milestone
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Title = "Test Milestone",
                Amount = 1000,
                Status = MilestoneStatus.DISPUTED,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Milestones.Add(milestone);

            // Create payment with HELD status
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                MilestoneId = milestone.Id,
                ProjectId = project.Id,
                PayerId = client.Id,
                PayeeId = expert.Id,
                Amount = 1000,
                Status = PaymentStatus.HELD,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Payments.Add(payment);

            // Create dispute
            var dispute = new Dispute
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                MilestoneId = milestone.Id,
                PaymentId = payment.Id,
                OpenedBy = client.Id,
                AgainstUserId = expert.Id,
                Reason = "Test dispute",
                Status = DisputeStatus.OPEN,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Disputes.Add(dispute);

            await dbContext.SaveChangesAsync();

            // Get services
            var disputeService = serviceProvider.GetRequiredService<IService>();
            var treasuryService = serviceProvider.GetRequiredService<ITreasury>();

            // Act - Resolve dispute with REQUEST_REVISION
            var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
            {
                ResolutionType = DisputeResolutionType.REQUEST_REVISION,
                ResolutionNote = "Request revision for milestone"
            };

            // Act
            var response = await disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

            // Assert - Should succeed
            Assert.NotNull(response);
            Assert.Equal(DisputeStatus.RESOLVED.ToString(), response.Status);

            // Verify milestone is now REVISION_REQUESTED
            var updatedMilestone = await dbContext.Milestones.FindAsync(milestone.Id);
            Assert.NotNull(updatedMilestone);
            Assert.Equal(MilestoneStatus.REVISION_REQUESTED, updatedMilestone.Status);

            // Verify payment status should still be HELD (not changed to FROZEN)
            var updatedPayment = await dbContext.Payments.FindAsync(payment.Id);
            Assert.NotNull(updatedPayment);
            Assert.Equal(PaymentStatus.HELD, updatedPayment.Status);
        }
    }
}
