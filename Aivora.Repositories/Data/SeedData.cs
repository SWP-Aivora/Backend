using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Microsoft.EntityFrameworkCore;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Aivora.Repositories.Data;

public static class SeedData
{
    public static async Task Initialize(AivoraDbContext context, bool forceReset = false)
    {
        // 1. Kiểm tra Reset nếu cần
        if (forceReset)
        {
            context.Reviews.RemoveRange(context.Reviews);
            context.Deliverables.RemoveRange(context.Deliverables);
            context.Milestones.RemoveRange(context.Milestones);
            context.Projects.RemoveRange(context.Projects);
            context.Proposals.RemoveRange(context.Proposals);
            context.JobPosts.RemoveRange(context.JobPosts);
            context.ExpertSkills.RemoveRange(context.ExpertSkills);
            context.Skills.RemoveRange(context.Skills);
            context.Categories.RemoveRange(context.Categories);
            context.ExpertProfiles.RemoveRange(context.ExpertProfiles);
            context.ClientProfiles.RemoveRange(context.ClientProfiles);
            context.Wallets.RemoveRange(context.Wallets);
            context.Users.RemoveRange(context.Users);
            await context.SaveChangesAsync();
        }

        // 2. Chế độ an toàn: Chỉ seed nếu chưa có User nào
        if (await context.Users.AnyAsync())
        {
            return;
        }

        // ── 1. Users & Wallets ──────────────────────────────────────
        var admin1 = new User { Email = "admin@aivora.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Platform Admin", Role = UserRole.ADMIN, Status = UserStatus.ACTIVE };
        var admin2 = new User { Email = "Ahihi@aivora.com", PasswordHash = BCryptNet.HashPassword("Ahihi123"), FullName = "Ahihi Admin", Role = UserRole.ADMIN, Status = UserStatus.ACTIVE };

        var clientStartup = new User { Email = "client.startup@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "TechNova Solutions", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE };
        var clientEcommerce = new User { Email = "client.ecommerce@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Glamour Boutique", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE };
        var clientResearch = new User { Email = "client.research@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "John Doe", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE };

        var expertSeniorAI = new User { Email = "expert.senior.ai@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Dr. Evelyn Reed", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE };
        var expertFullstack = new User { Email = "expert.fullstack@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Marcus Chen", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE };
        var expertDataScientist = new User { Email = "expert.data.scientist@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Isabella Rossi", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE };
        var expertAutomation = new User { Email = "expert.automation@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Kenji Tanaka", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE };
        var expertJuniorAI = new User { Email = "expert.junior.ai@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Ben Carter", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE };

        var users = new[] { admin1, admin2, clientStartup, clientEcommerce, clientResearch, expertSeniorAI, expertFullstack, expertDataScientist, expertAutomation, expertJuniorAI };
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var wallets = users.Select(u => new Wallet { UserId = u.Id, AvailableBalance = (u.Role == UserRole.CLIENT ? 10000m : 0m), Currency = "AICOIN" }).ToList();
        context.Wallets.AddRange(wallets);
        await context.SaveChangesAsync();

        // ── 2. Profiles ─────────────────────────────────────────────
        context.ClientProfiles.AddRange(
            new ClientProfile { UserId = clientStartup.Id, CompanyName = "TechNova Solutions" },
            new ClientProfile { UserId = clientEcommerce.Id, CompanyName = "Glamour Boutique" },
            new ClientProfile { UserId = clientResearch.Id, CompanyName = "Independent Researcher" }
        );
        context.ExpertProfiles.AddRange(
            new ExpertProfile { UserId = expertSeniorAI.Id, Title = "Principal AI Engineer", Bio = "10+ years in ML.", HourlyRate = 150 },
            new ExpertProfile { UserId = expertFullstack.Id, Title = "Full-Stack Developer | AI Integrator", Bio = "Building scalable web apps with AI.", HourlyRate = 90 },
            new ExpertProfile { UserId = expertDataScientist.Id, Title = "Data Scientist", Bio = "Turning data into insights.", HourlyRate = 120 },
            new ExpertProfile { UserId = expertAutomation.Id, Title = "Automation Specialist", Bio = "Automating business processes.", HourlyRate = 80 },
            new ExpertProfile { UserId = expertJuniorAI.Id, Title = "AI Developer", Bio = "Eager to build great AI products.", HourlyRate = 50 }
        );
        await context.SaveChangesAsync();

        // ── 3. Taxonomy ─────────────────────────────────────────────
        var catChatbot = new Category { Name = "AI Chatbots" };
        var catData = new Category { Name = "Data Science & Analytics" };
        var catWeb = new Category { Name = "Web & AI Integration" };

        context.Categories.AddRange(catChatbot, catData, catWeb);
        await context.SaveChangesAsync();

        var skillPython = new Skill { Name = "Python", CategoryId = catData.Id };
        var skillRAG = new Skill { Name = "RAG", CategoryId = catChatbot.Id };
        var skillLangChain = new Skill { Name = "LangChain", CategoryId = catChatbot.Id };
        var skillReact = new Skill { Name = "React", CategoryId = catWeb.Id };
        var skillSQL = new Skill { Name = "SQL", CategoryId = catData.Id };

        context.Skills.AddRange(skillPython, skillRAG, skillLangChain, skillReact, skillSQL);
        await context.SaveChangesAsync();

        var seniorAIProfile = context.ExpertProfiles.First(p => p.UserId == expertSeniorAI.Id);
        var fullstackProfile = context.ExpertProfiles.First(p => p.UserId == expertFullstack.Id);
        var dataScientistProfile = context.ExpertProfiles.First(p => p.UserId == expertDataScientist.Id);

        context.ExpertSkills.AddRange(
            new ExpertSkill { ExpertId = seniorAIProfile.Id, SkillId = skillPython.Id, Level = SkillLevel.EXPERT },
            new ExpertSkill { ExpertId = seniorAIProfile.Id, SkillId = skillRAG.Id, Level = SkillLevel.EXPERT },
            new ExpertSkill { ExpertId = fullstackProfile.Id, SkillId = skillReact.Id, Level = SkillLevel.ADVANCED },
            new ExpertSkill { ExpertId = fullstackProfile.Id, SkillId = skillPython.Id, Level = SkillLevel.INTERMEDIATE },
            new ExpertSkill { ExpertId = dataScientistProfile.Id, SkillId = skillSQL.Id, Level = SkillLevel.EXPERT },
            new ExpertSkill { ExpertId = dataScientistProfile.Id, SkillId = skillPython.Id, Level = SkillLevel.ADVANCED }
        );
        await context.SaveChangesAsync();

        // ── 4. Job 1: Open for Bidding ──────────────────────────────
        var jobChatbot = new JobPost
        {
            ClientId = clientStartup.Id,
            Title = "Build a Customer Support Chatbot for a SaaS Product",
            OriginalDescription = "We need an intelligent chatbot that can answer user questions based on our knowledge base.",
            FinalDescription = "We need an intelligent chatbot that can answer user questions based on our knowledge base.",
            Status = JobStatus.OPEN,
            BudgetMin = 2000,
            BudgetMax = 5000,
            Currency = "AICOIN"
        };
        context.JobPosts.Add(jobChatbot);
        await context.SaveChangesAsync();

        context.Proposals.AddRange(
            new Proposal { JobId = jobChatbot.Id, ExpertId = expertSeniorAI.Id, CoverLetter = "I have extensive experience building chatbots with RAG and LangChain.", ProposedBudget = 4500 },
            new Proposal { JobId = jobChatbot.Id, ExpertId = expertFullstack.Id, CoverLetter = "I can build and integrate this chatbot into your existing platform.", ProposedBudget = 3000 }
        );
        await context.SaveChangesAsync();

        // ── 5. Job 2: In Progress ───────────────────────────────────
        var jobInProgress = new JobPost
        {
            ClientId = clientEcommerce.Id,
            Title = "E-commerce Product Recommendation Engine",
            OriginalDescription = "Build a product recommendation engine for our e-commerce platform.",
            Status = JobStatus.IN_PROGRESS,
            BudgetMin = 3000,
            BudgetMax = 8000,
            Currency = "AICOIN"
        };
        context.JobPosts.Add(jobInProgress);
        await context.SaveChangesAsync();

        var proposalAccepted = new Proposal
        {
            JobId = jobInProgress.Id,
            ExpertId = expertSeniorAI.Id,
            Status = ProposalStatus.ACCEPTED,
            CoverLetter = "I have extensive experience building recommendation systems.",
            ProposedBudget = 6000
        };
        context.Proposals.Add(proposalAccepted);
        await context.SaveChangesAsync();

        var projectInProgress = new Project
        {
            JobId = jobInProgress.Id,
            AcceptedProposalId = proposalAccepted.Id,
            ClientId = clientEcommerce.Id,
            ExpertId = expertSeniorAI.Id,
            Title = jobInProgress.Title,
            Status = ProjectStatus.ACTIVE
        };
        projectInProgress.Currency = "AICOIN";
        context.Projects.Add(projectInProgress);
        await context.SaveChangesAsync();

        var m1_paid = new Milestone { ProjectId = projectInProgress.Id, Title = "Data Analysis & Model Design", Amount = 1500, Status = MilestoneStatus.PAID, FundedAt = DateTime.UtcNow.AddDays(-10), ApprovedAt = DateTime.UtcNow.AddDays(-5) };
        var m2_submitted = new Milestone { ProjectId = projectInProgress.Id, Title = "Backend API Implementation", Amount = 3000, Status = MilestoneStatus.SUBMITTED, FundedAt = DateTime.UtcNow.AddDays(-4) };
        var m3_pending = new Milestone { ProjectId = projectInProgress.Id, Title = "Frontend Integration & Testing", Amount = 1500, Status = MilestoneStatus.CREATED };
        context.Milestones.AddRange(m1_paid, m2_submitted, m3_pending);
        await context.SaveChangesAsync();

        context.Deliverables.Add(new Deliverable
        {
            MilestoneId = m2_submitted.Id,
            ExpertId = expertSeniorAI.Id,
            Description = "API endpoints are ready for review.",
            Status = DeliverableStatus.SUBMITTED
        });
        await context.SaveChangesAsync();

        // ── 6. Job 3: Completed ─────────────────────────────────────
        var jobCompleted = new JobPost
        {
            ClientId = clientResearch.Id,
            Title = "Analyze Sales Data for Q2 Report",
            OriginalDescription = "Analyze our Q2 sales data and produce a comprehensive report.",
            Status = JobStatus.COMPLETED,
            BudgetMin = 500,
            BudgetMax = 1000,
            Currency = "AICOIN"
        };
        context.JobPosts.Add(jobCompleted);
        await context.SaveChangesAsync();

        var proposalCompleted = new Proposal
        {
            JobId = jobCompleted.Id,
            ExpertId = expertDataScientist.Id,
            Status = ProposalStatus.ACCEPTED,
            CoverLetter = "I can deliver a thorough analysis.",
            ProposedBudget = 800
        };
        context.Proposals.Add(proposalCompleted);
        await context.SaveChangesAsync();

        var projectCompleted = new Project
        {
            JobId = jobCompleted.Id,
            AcceptedProposalId = proposalCompleted.Id,
            ClientId = clientResearch.Id,
            ExpertId = expertDataScientist.Id,
            Title = jobCompleted.Title,
            Status = ProjectStatus.COMPLETED,
            CompletedAt = DateTime.UtcNow.AddDays(-2),
            Currency = "AICOIN"
        };
        context.Projects.Add(projectCompleted);
        await context.SaveChangesAsync();

        var m_completed = new Milestone { ProjectId = projectCompleted.Id, Title = "Full Analysis and Report", Amount = 800, Status = MilestoneStatus.PAID };
        context.Milestones.Add(m_completed);
        await context.SaveChangesAsync();

        context.Reviews.AddRange(
            new Review { ProjectId = projectCompleted.Id, ReviewerId = clientResearch.Id, RevieweeId = expertDataScientist.Id, Rating = 5, Comment = "Excellent work, very thorough analysis!" },
            new Review { ProjectId = projectCompleted.Id, ReviewerId = expertDataScientist.Id, RevieweeId = clientResearch.Id, Rating = 5, Comment = "Great client, very clear requirements." }
        );
        await context.SaveChangesAsync();
    }
}
