using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.FinancialLedger;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests.Services;

public class E2EBusinessFlowTests
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
    public async Task E2E_HappyPath_ChatbotBillingWorkflow_Succeeds()
    {
        // ----------------------------------------------------
        // Arrange & Preconditions
        // ----------------------------------------------------
        var dbContext = GetDbContext();
        
        var clientId = Guid.NewGuid(); // Khang
        var expertId = Guid.NewGuid(); // QAnh
        var adminId = Guid.NewGuid();  // Quân

        var khangUser = new User { Id = clientId, FullName = "Khang", Role = UserRole.CLIENT, Email = "khang@aivora.com", PasswordHash = "hash" };
        var qanhUser = new User { Id = expertId, FullName = "QAnh", Role = UserRole.EXPERT, Email = "qanh@aivora.com", PasswordHash = "hash" };
        var quanUser = new User { Id = adminId, FullName = "Quân", Role = UserRole.ADMIN, Email = "quan@aivora.com", PasswordHash = "hash" };

        var khangProfile = new ClientProfile { UserId = clientId, CompanyName = "Beauty Shop", Rating = 0, TotalReviews = 0 };
        var qanhProfile = new ExpertProfile { UserId = expertId, Title = "AI Developer", Rating = 0, TotalReviews = 0, ExperienceYears = 3 };

        // Precondition: Khang's wallet starts with 2,000 Coin
        var khangWallet = new Wallet { UserId = clientId, AvailableBalance = 2000, HeldBalance = 0, Currency = "AICOIN" };
        var qanhWallet = new Wallet { UserId = expertId, AvailableBalance = 0, HeldBalance = 0, Currency = "AICOIN" };

        dbContext.Users.AddRange(khangUser, qanhUser, quanUser);
        dbContext.ClientProfiles.Add(khangProfile);
        dbContext.ExpertProfiles.Add(qanhProfile);
        dbContext.Wallets.AddRange(khangWallet, qanhWallet);
        
        var category = new Category { Id = Guid.NewGuid(), Name = "AI Chatbots" };
        dbContext.Categories.Add(category);

        var skill1 = new Skill { Id = Guid.NewGuid(), Name = "OpenAI API" };
        var skill2 = new Skill { Id = Guid.NewGuid(), Name = "Chatbot" };
        var skill3 = new Skill { Id = Guid.NewGuid(), Name = "React" };
        dbContext.Skills.AddRange(skill1, skill2, skill3);
        await dbContext.SaveChangesAsync();

        // ----------------------------------------------------
        // 1. Client Create Job — Khang
        // ----------------------------------------------------
        var jobService = new Aivora.Services.JobService.Service(dbContext);
        
        var createJobReq = new Aivora.Services.JobService.Request.CreateJobRequest
        {
            Title = "Build AI Chatbot for Beauty Shop",
            OriginalDescription = "Need chatbot to answer product questions and recommend skincare products",
            CategoryId = category.Id,
            BudgetType = BudgetType.FIXED,
            BudgetMin = 800,
            BudgetMax = 1000,
            TimelineDays = 14,
            Visibility = JobVisibility.PUBLIC,
            SkillIds = new List<Guid> { skill1.Id, skill2.Id, skill3.Id }
        };

        var jobResponse = await jobService.CreateJobAsync(clientId, createJobReq);
        jobResponse.Status.Should().Be(JobStatus.DRAFT);

        // Publish job -> Open status
        var publishedJob = await jobService.PublishJobAsync(clientId, jobResponse.Id);
        publishedJob.Status.Should().Be(JobStatus.OPEN);

        // ----------------------------------------------------
        // 2. Expert Applies for Job — QAnh
        // ----------------------------------------------------
        // We simulate proposal creation in the database
        var proposalId = Guid.NewGuid();
        var proposal = new Proposal
        {
            Id = proposalId,
            JobId = publishedJob.Id,
            ExpertId = expertId,
            CoverLetter = "I can build this chatbot using OpenAI API and React.",
            ProposedBudget = 900,
            ProposedTimelineDays = 14,
            Status = ProposalStatus.SUBMITTED,
            Currency = "AICOIN",
            Milestones = new List<ProposalMilestone>
            {
                new ProposalMilestone
                {
                    Title = "Chatbot MVP Delivery",
                    Description = "Chatbot can answer FAQ, recommend products, and provide demo URL",
                    Amount = 900,
                    DueDays = 14,
                    AcceptanceCriteria = "Works as described",
                    OrderIndex = 1
                }
            }
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync();

        // Client accepts the proposal
        var hiringWorkflowService = new Aivora.Services.HiringWorkflowService.Service(dbContext);
        var hiringResult = await hiringWorkflowService.AcceptProposalAsync(clientId, proposalId);

        hiringResult.Status.Should().Be(ProjectStatus.PENDING_PAYMENT.ToString());
        
        var acceptedJob = await dbContext.JobPosts.FindAsync(publishedJob.Id);
        acceptedJob!.Status.Should().Be(JobStatus.IN_PROGRESS);

        var acceptedProposal = await dbContext.Proposals.FindAsync(proposalId);
        acceptedProposal!.Status.Should().Be(ProposalStatus.ACCEPTED);

        // ----------------------------------------------------
        // 3. Project Management — Escrow Funding
        // ----------------------------------------------------
        var project = await dbContext.Projects.Include(p => p.Milestones).FirstAsync(p => p.Id == hiringResult.ProjectId);
        project.Status.Should().Be(ProjectStatus.PENDING_PAYMENT);
        project.Milestones.Should().HaveCount(1);
        
        var milestone = project.Milestones.First();
        milestone.Status.Should().Be(MilestoneStatus.CREATED);

        var ledger = new FinancialLedger(dbContext);
        var milestoneService = new Aivora.Services.MilestoneService.Service(dbContext, ledger);

        // Client funds milestone
        var fundResult = await milestoneService.FundMilestoneAsync(clientId, milestone.Id);
        
        // Assert Wallet and project status updates
        fundResult.Milestone.Status.Should().Be(MilestoneStatus.FUNDED);
        fundResult.Wallet.AvailableBalance.Should().Be(1100);
        fundResult.Wallet.HeldBalance.Should().Be(900);

        var activeProject = await dbContext.Projects.FindAsync(project.Id);
        activeProject!.Status.Should().Be(ProjectStatus.ACTIVE);

        var activePayment = await dbContext.Payments.FirstAsync(p => p.MilestoneId == milestone.Id);
        activePayment.Status.Should().Be(PaymentStatus.HELD);

        // ----------------------------------------------------
        // 4. E2E Step 4.1 — Expert Submits Deliverable (QAnh)
        // ----------------------------------------------------
        var deliverableService = new Aivora.Services.DeliverableService.Service(dbContext);
        var submitDeliverableReq = new Aivora.Services.DeliverableService.Request.SubmitDeliverableRequest
        {
            Description = "Chatbot MVP completed with FAQ, product recommendation, and admin prompt config.",
            DemoUrl = "https://demo.beauty-chatbot.com",
            SourceCodeUrl = "https://github.com/demo/beauty-chatbot",
            Note = "Done"
        };

        var deliverableResponse = await deliverableService.SubmitDeliverableAsync(expertId, milestone.Id, submitDeliverableReq);
        deliverableResponse.Status.Should().Be(DeliverableStatus.SUBMITTED);
        deliverableResponse.RevisionNumber.Should().Be(1);

        var submittedMilestone = await dbContext.Milestones.FindAsync(milestone.Id);
        submittedMilestone!.Status.Should().Be(MilestoneStatus.SUBMITTED);
        submittedMilestone.SubmittedAt.Should().NotBeNull();

        // ----------------------------------------------------
        // 5. E2E Step 4.2 & 4.3 — Client Reviews & Approves Deliverable (Khang)
        // ----------------------------------------------------
        var approvedMilestone = await milestoneService.ApproveMilestoneAsync(clientId, milestone.Id);
        approvedMilestone.Status.Should().Be(MilestoneStatus.PAID);

        // ----------------------------------------------------
        // 6. E2E Step 4.4 & 4.5 — System Releases Payment & Completes Project
        // ----------------------------------------------------
        var updatedClientWallet = await dbContext.Wallets.FirstAsync(w => w.UserId == clientId);
        var updatedExpertWallet = await dbContext.Wallets.FirstAsync(w => w.UserId == expertId);

        updatedClientWallet.AvailableBalance.Should().Be(1100);
        updatedClientWallet.HeldBalance.Should().Be(0);

        updatedExpertWallet.AvailableBalance.Should().Be(900);
        updatedExpertWallet.TotalEarned.Should().Be(900);

        var updatedPayment = await dbContext.Payments.FirstAsync(p => p.MilestoneId == milestone.Id);
        updatedPayment.Status.Should().Be(PaymentStatus.RELEASED);
        updatedPayment.ReleasedAt.Should().NotBeNull();

        var completedProject = await dbContext.Projects.FindAsync(project.Id);
        completedProject!.Status.Should().Be(ProjectStatus.COMPLETED);
        completedProject.CompletedAt.Should().NotBeNull();

        var completedJob = await dbContext.JobPosts.FindAsync(publishedJob.Id);
        completedJob!.Status.Should().Be(JobStatus.IN_PROGRESS);

        // ----------------------------------------------------
        // 7. E2E Step 4.6 & 4.7 — Client & Expert Leave Reviews
        // ----------------------------------------------------
        var reviewService = new Aivora.Services.ReviewService.Service(dbContext);
        
        var clientReviewReq = new Aivora.Services.ReviewService.Request.CreateReviewRequest
        {
            ProjectId = project.Id,
            RevieweeId = expertId,
            Rating = 5,
            Comment = "Expert delivered a working chatbot on time with good quality.",
            CommunicationRating = 5,
            QualityRating = 5,
            DeadlineRating = 5
        };
        var clientReviewRes = await reviewService.CreateReviewAsync(clientId, clientReviewReq);
        clientReviewRes.Rating.Should().Be(5);

        var expertReviewReq = new Aivora.Services.ReviewService.Request.CreateReviewRequest
        {
            ProjectId = project.Id,
            RevieweeId = clientId,
            Rating = 5,
            Comment = "Client provided clear requirements and fast feedback.",
            CommunicationRating = 5,
            RequirementClarityRating = 5
        };
        var expertReviewRes = await reviewService.CreateReviewAsync(expertId, expertReviewReq);
        expertReviewRes.Rating.Should().Be(5);

        // Verify final rating calculations on profiles
        var finalExpertProfile = await dbContext.ExpertProfiles.FirstAsync(p => p.UserId == expertId);
        finalExpertProfile.Rating.Should().Be(5);
        finalExpertProfile.TotalReviews.Should().Be(1);

        var finalClientProfile = await dbContext.ClientProfiles.FirstAsync(p => p.UserId == clientId);
        finalClientProfile.Rating.Should().Be(5);
        finalClientProfile.TotalReviews.Should().Be(1);
    }

    [Fact]
    public async Task E2E_NegativeAndAlternativePaths_BehavesCorrectly()
    {
        // ----------------------------------------------------
        // Setup
        // ----------------------------------------------------
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var clientUser = new User { Id = clientId, FullName = "Client", Role = UserRole.CLIENT, Email = "c@t.com", PasswordHash = "x" };
        var expertUser = new User { Id = expertId, FullName = "Expert", Role = UserRole.EXPERT, Email = "e@t.com", PasswordHash = "x" };
        var adminUser = new User { Id = adminId, FullName = "Admin", Role = UserRole.ADMIN, Email = "a@t.com", PasswordHash = "x" };

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, Currency = "AICOIN" };

        var project = new Project { Id = Guid.NewGuid(), ClientId = clientId, ExpertId = expertId, Title = "E2E Alternative", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = Guid.NewGuid(), ProjectId = project.Id, Amount = 500, Status = MilestoneStatus.FUNDED, Title = "Milestone 1" };
        var payment = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = clientId, PayeeId = expertId, Amount = 500, Status = PaymentStatus.HELD };

        dbContext.Users.AddRange(clientUser, expertUser, adminUser);
        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var ledger = new FinancialLedger(dbContext);
        var milestoneService = new Aivora.Services.MilestoneService.Service(dbContext, ledger);
        var reviewService = new Aivora.Services.ReviewService.Service(dbContext);
        var disputeService = new Aivora.Services.DisputeService.Service(dbContext, ledger);

        // ----------------------------------------------------
        // Negative Test 1: Release payment before deliverable approval (Milestone is FUNDED, not SUBMITTED)
        // ----------------------------------------------------
        Func<Task> releaseBeforeApproval = async () => await milestoneService.ApproveMilestoneAsync(clientId, milestone.Id);
        await releaseBeforeApproval.Should().ThrowAsync<ValidationException>()
            .WithMessage("Milestone must be in SUBMITTED status to be approved.");

        // ----------------------------------------------------
        // Negative Test 2: Review before project is completed
        // ----------------------------------------------------
        var earlyReviewReq = new Aivora.Services.ReviewService.Request.CreateReviewRequest
        {
            ProjectId = project.Id,
            RevieweeId = expertId,
            Rating = 5,
            Comment = "Too early"
        };
        Func<Task> earlyReview = async () => await reviewService.CreateReviewAsync(clientId, earlyReviewReq);
        await earlyReview.Should().ThrowAsync<ValidationException>()
            .WithMessage("Reviews can only be given for completed projects.");

        // Mark project completed to unlock review-related negative checks
        project.Status = ProjectStatus.COMPLETED;
        await dbContext.SaveChangesAsync();

        // ----------------------------------------------------
        // Negative Test 4: User cannot review themselves
        // ----------------------------------------------------
        var selfReviewReq = new Aivora.Services.ReviewService.Request.CreateReviewRequest
        {
            ProjectId = project.Id,
            RevieweeId = clientId,
            Rating = 5,
            Comment = "Self love"
        };
        Func<Task> selfReview = async () => await reviewService.CreateReviewAsync(clientId, selfReviewReq);
        await selfReview.Should().ThrowAsync<ValidationException>()
            .WithMessage("You cannot review yourself.");

        // ----------------------------------------------------
        // Negative Test 5: Duplicate review for same project/reviewer
        // ----------------------------------------------------
        var validReviewReq = new Aivora.Services.ReviewService.Request.CreateReviewRequest
        {
            ProjectId = project.Id,
            RevieweeId = expertId,
            Rating = 5,
            Comment = "Good work"
        };
        await reviewService.CreateReviewAsync(clientId, validReviewReq);

        Func<Task> duplicateReview = async () => await reviewService.CreateReviewAsync(clientId, validReviewReq);
        await duplicateReview.Should().ThrowAsync<ValidationException>()
            .WithMessage("You have already reviewed this project.");

        // ----------------------------------------------------
        // Setup Alternative Flow: Revision Request
        // ----------------------------------------------------
        // Restore project & milestone to submitted state
        project.Status = ProjectStatus.ACTIVE;
        milestone.Status = MilestoneStatus.SUBMITTED;
        payment.Status = PaymentStatus.HELD;
        await dbContext.SaveChangesAsync();

        // Client requests revision
        var revisionRes = await milestoneService.RequestRevisionAsync(clientId, milestone.Id, "Please improve the landing UI.");
        revisionRes.Status.Should().Be(MilestoneStatus.REVISION_REQUESTED);
        
        // Escrow payment remains held
        var heldPayment = await dbContext.Payments.FindAsync(payment.Id);
        heldPayment!.Status.Should().Be(PaymentStatus.HELD);

        // ----------------------------------------------------
        // Setup Alternative Flow: Dispute Resolution
        // ----------------------------------------------------
        // Restore to funded state to open dispute
        milestone.Status = MilestoneStatus.FUNDED;
        await dbContext.SaveChangesAsync();

        var openDisputeReq = new Aivora.Services.DisputeService.Request.OpenDisputeRequest
        {
            MilestoneId = milestone.Id,
            Reason = "No progress"
        };
        
        var disputeResult = await disputeService.OpenDisputeAsync(clientId, openDisputeReq);
        disputeResult.Status.Should().Be(DisputeStatus.OPEN.ToString());

        var disputedMilestone = await dbContext.Milestones.FindAsync(milestone.Id);
        disputedMilestone!.Status.Should().Be(MilestoneStatus.DISPUTED);

        var disputedProject = await dbContext.Projects.FindAsync(project.Id);
        disputedProject!.Status.Should().Be(ProjectStatus.DISPUTED);

        var frozenPayment = await dbContext.Payments.FindAsync(payment.Id);
        frozenPayment!.Status.Should().Be(PaymentStatus.FROZEN);
    }
}
