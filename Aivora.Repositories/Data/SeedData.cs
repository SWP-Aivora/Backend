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
            // Finance & Communication (Leaf nodes)
            context.WalletTransactions.RemoveRange(context.WalletTransactions);
            context.Payments.RemoveRange(context.Payments);
            context.Notifications.RemoveRange(context.Notifications);
            context.Messages.RemoveRange(context.Messages);
            context.Conversations.RemoveRange(context.Conversations);
            context.Reviews.RemoveRange(context.Reviews);
            context.DisputeEvidences.RemoveRange(context.DisputeEvidences);
            context.Disputes.RemoveRange(context.Disputes);

            // Projects & Proposals
            context.Deliverables.RemoveRange(context.Deliverables);
            context.Milestones.RemoveRange(context.Milestones);
            context.Projects.RemoveRange(context.Projects);
            context.ProposalMilestones.RemoveRange(context.ProposalMilestones);
            context.Proposals.RemoveRange(context.Proposals);

            // Jobs
            context.JobPostMilestones.RemoveRange(context.JobPostMilestones);
            context.JobSkills.RemoveRange(context.JobSkills);
            context.AIJobSuggestions.RemoveRange(context.AIJobSuggestions);
            context.JobPosts.RemoveRange(context.JobPosts);
            context.RecommendationResults.RemoveRange(context.RecommendationResults);

            // Profiles & Taxonomy
            context.ExpertSkills.RemoveRange(context.ExpertSkills);
            context.Skills.RemoveRange(context.Skills);
            context.Categories.RemoveRange(context.Categories);
            context.ExpertProfiles.RemoveRange(context.ExpertProfiles);
            context.ClientProfiles.RemoveRange(context.ClientProfiles);

            // Identity (Root nodes)
            context.Wallets.RemoveRange(context.Wallets);
            context.Users.RemoveRange(context.Users);

            await context.SaveChangesAsync();
            // Clear EF Core tracking state to ensure fresh query for existing users
            context.ChangeTracker.Clear();
        }
        else
        {
            // For InMemory DB used in tests, still seed data when forceReset=false
            // because there's no persistence between test runs
            var userCount = await context.Users.CountAsync();
            if (userCount == 0)
            {
                forceReset = true;
            }
        }

        // 2. Chỉ seed khi forceReset=true để tránh duplicate constraint errors
        if (!forceReset)
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

        // Lấy danh sách user hiện có từ database (không từ memory)
        var existingUsers = await context.Users.AsNoTracking().ToListAsync();
        var users = new List<User>();

        // Chỉ thêm user nếu chưa tồn tại
        foreach (var user in new[] { admin1, admin2, clientStartup, clientEcommerce, clientResearch, expertSeniorAI, expertFullstack, expertDataScientist, expertAutomation, expertJuniorAI })
        {
            if (!existingUsers.Any(u => u.Email == user.Email))
            {
                // Setup wallet for user
                decimal available = 0m;
                decimal held = 0m;
                decimal earned = 0m;

                if (user.Email == "client.startup@demo.com") { available = 6500m; held = 1000m; }
                else if (user.Email == "client.ecommerce@demo.com") { available = 5500m; held = 3000m; }
                else if (user.Email == "client.research@demo.com") { available = 9200m; held = 0m; }
                else if (user.Role == UserRole.CLIENT) { available = 10000m; }
                else if (user.Email == "expert.senior.ai@demo.com") { available = 1500m; earned = 1500m; }
                else if (user.Email == "expert.data.scientist@demo.com") { available = 800m; earned = 800m; }
                else if (user.Email == "expert.automation@demo.com") { available = 2500m; earned = 2500m; }

                user.Wallet = new Wallet
                {
                    UserId = user.Id,
                    AvailableBalance = available,
                    HeldBalance = held,
                    TotalEarned = earned,
                    Currency = "AICOIN"
                };
                users.Add(user);
            }
        }


        // Chỉ add nếu có user mới
        if (users.Any())
        {
            context.Users.AddRange(users);
        }
        await context.SaveChangesAsync();

        // ── 2. Profiles (chỉ nếu có user mới được insert) ─────────────────────────────────────────────
        if (users.Any())
        {
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
        }
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
        var skillSelenium = new Skill { Name = "Selenium", CategoryId = catWeb.Id };
        var skillZapier = new Skill { Name = "Zapier", CategoryId = catWeb.Id };

        context.Skills.AddRange(skillPython, skillRAG, skillLangChain, skillReact, skillSQL, skillSelenium, skillZapier);
        await context.SaveChangesAsync();

        // ── 3. Skills & Expert Skills (chỉ nếu có user mới) ────────────────────────────────────────
        if (users.Any())
        {
            // Skills đã được add ở trên, không cần add lại

            var seniorAIProfile = context.ExpertProfiles.First(p => p.UserId == expertSeniorAI.Id);
            var fullstackProfile = context.ExpertProfiles.First(p => p.UserId == expertFullstack.Id);
            var dataScientistProfile = context.ExpertProfiles.First(p => p.UserId == expertDataScientist.Id);
            var automationProfile = context.ExpertProfiles.First(p => p.UserId == expertAutomation.Id);

            context.ExpertSkills.AddRange(
                    new ExpertSkill { ExpertId = seniorAIProfile.Id, SkillId = skillPython.Id, Level = SkillLevel.EXPERT },
                    new ExpertSkill { ExpertId = seniorAIProfile.Id, SkillId = skillRAG.Id, Level = SkillLevel.EXPERT },
                    new ExpertSkill { ExpertId = fullstackProfile.Id, SkillId = skillReact.Id, Level = SkillLevel.ADVANCED },
                    new ExpertSkill { ExpertId = fullstackProfile.Id, SkillId = skillPython.Id, Level = SkillLevel.INTERMEDIATE },
                    new ExpertSkill { ExpertId = dataScientistProfile.Id, SkillId = skillSQL.Id, Level = SkillLevel.EXPERT },
                    new ExpertSkill { ExpertId = dataScientistProfile.Id, SkillId = skillPython.Id, Level = SkillLevel.ADVANCED },
                    new ExpertSkill { ExpertId = automationProfile.Id, SkillId = skillPython.Id, Level = SkillLevel.ADVANCED },
                    new ExpertSkill { ExpertId = automationProfile.Id, SkillId = skillSelenium.Id, Level = SkillLevel.EXPERT },
                    new ExpertSkill { ExpertId = automationProfile.Id, SkillId = skillZapier.Id, Level = SkillLevel.EXPERT }
                );
            await context.SaveChangesAsync();
        }

        // ── 4. Job 1: Open for Bidding (Original) ───────────────────
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

        // ── 5. Job 2: In Progress (Original - Ecommerce) ────────────
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
            Status = ProjectStatus.ACTIVE,
            Currency = "AICOIN"
        };
        context.Projects.Add(projectInProgress);
        await context.SaveChangesAsync();

        var m1_paid = new Milestone { ProjectId = projectInProgress.Id, Title = "Data Analysis & Model Design", Amount = 1500, Status = MilestoneStatus.PAID, FundedAt = DateTime.UtcNow.AddDays(-10), ApprovedAt = DateTime.UtcNow.AddDays(-5), PaidAt = DateTime.UtcNow.AddDays(-5) };
        var m2_submitted = new Milestone { ProjectId = projectInProgress.Id, Title = "Backend API Implementation", Amount = 3000, Status = MilestoneStatus.DISPUTED, FundedAt = DateTime.UtcNow.AddDays(-4) };
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

        var paymentOld1 = new Payment { ProjectId = projectInProgress.Id, MilestoneId = m1_paid.Id, PayerId = clientEcommerce.Id, PayeeId = expertSeniorAI.Id, Amount = 1500, Status = PaymentStatus.RELEASED, HeldAt = DateTime.UtcNow.AddDays(-10), ReleasedAt = DateTime.UtcNow.AddDays(-5) };
        var paymentOld2 = new Payment { ProjectId = projectInProgress.Id, MilestoneId = m2_submitted.Id, PayerId = clientEcommerce.Id, PayeeId = expertSeniorAI.Id, Amount = 3000, Status = PaymentStatus.HELD, HeldAt = DateTime.UtcNow.AddDays(-4) };
        context.Payments.AddRange(paymentOld1, paymentOld2);
        await context.SaveChangesAsync();

        // Dispute for Project In Progress
        var dispute = new Dispute
        {
            ProjectId = projectInProgress.Id,
            MilestoneId = m2_submitted.Id,
            PaymentId = paymentOld2.Id,
            OpenedBy = clientEcommerce.Id,
            AgainstUserId = expertSeniorAI.Id,
            Reason = "Quality of work",
            Description = "The deliverable API endpoints return 500 errors on key product categories and do not match the spec.",
            Status = DisputeStatus.OPEN
        };
        context.Disputes.Add(dispute);
        await context.SaveChangesAsync();

        context.DisputeEvidences.AddRange(
            new DisputeEvidence { DisputeId = dispute.Id, SubmittedBy = clientEcommerce.Id, Content = "Here are the API logs showing the errors and request payloads...", FileUrl = "https://example.com/api_error_logs.txt" },
            new DisputeEvidence { DisputeId = dispute.Id, SubmittedBy = expertSeniorAI.Id, Content = "I tested the API locally and it works fine. The issue might be with their sandbox DB credentials.", FileUrl = "https://example.com/api_demo_video.mp4" }
        );
        await context.SaveChangesAsync();

        // ── 6. Job 3: Completed (Original - Research) ───────────────
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

        var m_completed = new Milestone { ProjectId = projectCompleted.Id, Title = "Full Analysis and Report", Amount = 800, Status = MilestoneStatus.PAID, FundedAt = DateTime.UtcNow.AddDays(-3), ApprovedAt = DateTime.UtcNow.AddDays(-2), PaidAt = DateTime.UtcNow.AddDays(-2) };
        context.Milestones.Add(m_completed);
        await context.SaveChangesAsync();

        var paymentOld3 = new Payment { ProjectId = projectCompleted.Id, MilestoneId = m_completed.Id, PayerId = clientResearch.Id, PayeeId = expertDataScientist.Id, Amount = 800, Status = PaymentStatus.RELEASED, HeldAt = DateTime.UtcNow.AddDays(-3), ReleasedAt = DateTime.UtcNow.AddDays(-2) };
        context.Payments.Add(paymentOld3);
        await context.SaveChangesAsync();

        context.Reviews.AddRange(
            new Review { ProjectId = projectCompleted.Id, ReviewerId = clientResearch.Id, RevieweeId = expertDataScientist.Id, Rating = 5, Comment = "Excellent work, very thorough analysis!" },
            new Review { ProjectId = projectCompleted.Id, ReviewerId = expertDataScientist.Id, RevieweeId = clientResearch.Id, Rating = 5, Comment = "Great client, very clear requirements." }
        );
        await context.SaveChangesAsync();

        // ── 7. Job 4: Completed (New - Automation) ───────────────────
        var jobAutomationCompleted = new JobPost
        {
            ClientId = clientStartup.Id,
            Title = "Data Pipeline Automation",
            OriginalDescription = "Build a Python-based scraping pipeline to collect sales data daily and export to CSV.",
            FinalDescription = "Build a Python-based scraping pipeline to collect sales data daily and export to CSV.",
            Status = JobStatus.COMPLETED,
            BudgetMin = 1000,
            BudgetMax = 2000,
            Currency = "AICOIN"
        };
        context.JobPosts.Add(jobAutomationCompleted);
        await context.SaveChangesAsync();

        var proposalAutoCompleted = new Proposal
        {
            JobId = jobAutomationCompleted.Id,
            ExpertId = expertAutomation.Id,
            Status = ProposalStatus.ACCEPTED,
            CoverLetter = "I have built similar web scrapers and ETL pipelines. Ready to start immediately.",
            ProposedBudget = 1500
        };
        context.Proposals.Add(proposalAutoCompleted);
        await context.SaveChangesAsync();

        var projectAutoCompleted = new Project
        {
            JobId = jobAutomationCompleted.Id,
            AcceptedProposalId = proposalAutoCompleted.Id,
            ClientId = clientStartup.Id,
            ExpertId = expertAutomation.Id,
            Title = jobAutomationCompleted.Title,
            Status = ProjectStatus.COMPLETED,
            CompletedAt = DateTime.UtcNow.AddDays(-3),
            Currency = "AICOIN"
        };
        context.Projects.Add(projectAutoCompleted);
        await context.SaveChangesAsync();

        var milestoneAuto1 = new Milestone { ProjectId = projectAutoCompleted.Id, Title = "Script Development", Amount = 700, Status = MilestoneStatus.PAID, FundedAt = DateTime.UtcNow.AddDays(-7), ApprovedAt = DateTime.UtcNow.AddDays(-5), PaidAt = DateTime.UtcNow.AddDays(-5) };
        var milestoneAuto2 = new Milestone { ProjectId = projectAutoCompleted.Id, Title = "Deployment & Setup", Amount = 800, Status = MilestoneStatus.PAID, FundedAt = DateTime.UtcNow.AddDays(-6), ApprovedAt = DateTime.UtcNow.AddDays(-3), PaidAt = DateTime.UtcNow.AddDays(-3) };
        context.Milestones.AddRange(milestoneAuto1, milestoneAuto2);
        await context.SaveChangesAsync();

        context.Deliverables.AddRange(
            new Deliverable { MilestoneId = milestoneAuto1.Id, ExpertId = expertAutomation.Id, Description = "Scraping script completed and verified", Status = DeliverableStatus.APPROVED, ReviewedAt = DateTime.UtcNow.AddDays(-5) },
            new Deliverable { MilestoneId = milestoneAuto2.Id, ExpertId = expertAutomation.Id, Description = "Deployment to VM complete", Status = DeliverableStatus.APPROVED, ReviewedAt = DateTime.UtcNow.AddDays(-3) }
        );
        await context.SaveChangesAsync();

        var paymentAuto1 = new Payment { ProjectId = projectAutoCompleted.Id, MilestoneId = milestoneAuto1.Id, PayerId = clientStartup.Id, PayeeId = expertAutomation.Id, Amount = 700, Status = PaymentStatus.RELEASED, HeldAt = DateTime.UtcNow.AddDays(-7), ReleasedAt = DateTime.UtcNow.AddDays(-5) };
        var paymentAuto2 = new Payment { ProjectId = projectAutoCompleted.Id, MilestoneId = milestoneAuto2.Id, PayerId = clientStartup.Id, PayeeId = expertAutomation.Id, Amount = 800, Status = PaymentStatus.RELEASED, HeldAt = DateTime.UtcNow.AddDays(-6), ReleasedAt = DateTime.UtcNow.AddDays(-3) };
        context.Payments.AddRange(paymentAuto1, paymentAuto2);
        await context.SaveChangesAsync();

        context.Reviews.AddRange(
            new Review { ProjectId = projectAutoCompleted.Id, ReviewerId = clientStartup.Id, RevieweeId = expertAutomation.Id, Rating = 5, Comment = "Kenji is an automation wizard! Highly recommended." },
            new Review { ProjectId = projectAutoCompleted.Id, ReviewerId = expertAutomation.Id, RevieweeId = clientStartup.Id, Rating = 5, Comment = "Great client, clear requirements, prompt payments." }
        );
        await context.SaveChangesAsync();

        // ── 8. Job 5: In Progress (New - Automation) ─────────────────
        var jobAutomationInProgress = new JobPost
        {
            ClientId = clientStartup.Id,
            Title = "CRM Zapier Workflow Automation",
            OriginalDescription = "Automate lead sync from our landing page webhook into HubSpot CRM using Zapier filters.",
            FinalDescription = "Automate lead sync from our landing page webhook into HubSpot CRM using Zapier filters.",
            Status = JobStatus.IN_PROGRESS,
            BudgetMin = 1500,
            BudgetMax = 3000,
            Currency = "AICOIN"
        };
        context.JobPosts.Add(jobAutomationInProgress);
        await context.SaveChangesAsync();

        var proposalAutoInProgress = new Proposal
        {
            JobId = jobAutomationInProgress.Id,
            ExpertId = expertAutomation.Id,
            Status = ProposalStatus.ACCEPTED,
            CoverLetter = "Certified Zapier expert here. I can set up complex filters and routing rules for HubSpot.",
            ProposedBudget = 2000
        };
        context.Proposals.Add(proposalAutoInProgress);
        await context.SaveChangesAsync();

        var projectAutoInProgress = new Project
        {
            JobId = jobAutomationInProgress.Id,
            AcceptedProposalId = proposalAutoInProgress.Id,
            ClientId = clientStartup.Id,
            ExpertId = expertAutomation.Id,
            Title = jobAutomationInProgress.Title,
            Status = ProjectStatus.ACTIVE,
            Currency = "AICOIN"
        };
        context.Projects.Add(projectAutoInProgress);
        await context.SaveChangesAsync();

        var milestoneAuto3 = new Milestone { ProjectId = projectAutoInProgress.Id, Title = "Zapier Trigger Integration", Amount = 1000, Status = MilestoneStatus.PAID, FundedAt = DateTime.UtcNow.AddDays(-4), ApprovedAt = DateTime.UtcNow.AddDays(-1), PaidAt = DateTime.UtcNow.AddDays(-1) };
        var milestoneAuto4 = new Milestone { ProjectId = projectAutoInProgress.Id, Title = "Action & Filtering Logic", Amount = 1000, Status = MilestoneStatus.FUNDED, FundedAt = DateTime.UtcNow.AddDays(-2) };
        context.Milestones.AddRange(milestoneAuto3, milestoneAuto4);
        await context.SaveChangesAsync();

        context.Deliverables.AddRange(
            new Deliverable { MilestoneId = milestoneAuto3.Id, ExpertId = expertAutomation.Id, Description = "Webhook trigger and parsing logic completed", Status = DeliverableStatus.APPROVED, ReviewedAt = DateTime.UtcNow.AddDays(-1) },
            new Deliverable { MilestoneId = milestoneAuto4.Id, ExpertId = expertAutomation.Id, Description = "Filtered logic and spreadsheet mapping. Please check.", Status = DeliverableStatus.SUBMITTED }
        );
        await context.SaveChangesAsync();

        var paymentAuto3 = new Payment { ProjectId = projectAutoInProgress.Id, MilestoneId = milestoneAuto3.Id, PayerId = clientStartup.Id, PayeeId = expertAutomation.Id, Amount = 1000, Status = PaymentStatus.RELEASED, HeldAt = DateTime.UtcNow.AddDays(-4), ReleasedAt = DateTime.UtcNow.AddDays(-1) };
        var paymentAuto4 = new Payment { ProjectId = projectAutoInProgress.Id, MilestoneId = milestoneAuto4.Id, PayerId = clientStartup.Id, PayeeId = expertAutomation.Id, Amount = 1000, Status = PaymentStatus.HELD, HeldAt = DateTime.UtcNow.AddDays(-2) };
        context.Payments.AddRange(paymentAuto3, paymentAuto4);
        await context.SaveChangesAsync();

        // ── 9. Wallet Transactions ───────────────────────────────────
        var walletStartup = clientStartup.Wallet!;
        var walletEcommerce = clientEcommerce.Wallet!;
        var walletResearch = clientResearch.Wallet!;
        var walletSeniorAI = expertSeniorAI.Wallet!;
        var walletDataScientist = expertDataScientist.Wallet!;
        var walletAutomation = expertAutomation.Wallet!;

        // Client Startup Transactions
        context.WalletTransactions.AddRange(
            new WalletTransaction { WalletId = walletStartup.Id, UserId = clientStartup.Id, Type = WalletTransactionType.DEMO_DEPOSIT, Direction = TransactionDirection.CREDIT, Amount = 10000, BalanceBefore = 0, BalanceAfter = 10000, Description = "Initial demo deposit" },
            new WalletTransaction { WalletId = walletStartup.Id, UserId = clientStartup.Id, PaymentId = paymentAuto1.Id, Type = WalletTransactionType.ESCROW_HOLD, Direction = TransactionDirection.DEBIT, Amount = 700, BalanceBefore = 10000, BalanceAfter = 9300, Description = "Escrow hold for Milestone: Script Development" },
            new WalletTransaction { WalletId = walletStartup.Id, UserId = clientStartup.Id, PaymentId = paymentAuto2.Id, Type = WalletTransactionType.ESCROW_HOLD, Direction = TransactionDirection.DEBIT, Amount = 800, BalanceBefore = 9300, BalanceAfter = 8500, Description = "Escrow hold for Milestone: Deployment & Setup" },
            new WalletTransaction { WalletId = walletStartup.Id, UserId = clientStartup.Id, PaymentId = paymentAuto3.Id, Type = WalletTransactionType.ESCROW_HOLD, Direction = TransactionDirection.DEBIT, Amount = 1000, BalanceBefore = 8500, BalanceAfter = 7500, Description = "Escrow hold for Milestone: Zapier Trigger Integration" },
            new WalletTransaction { WalletId = walletStartup.Id, UserId = clientStartup.Id, PaymentId = paymentAuto4.Id, Type = WalletTransactionType.ESCROW_HOLD, Direction = TransactionDirection.DEBIT, Amount = 1000, BalanceBefore = 7500, BalanceAfter = 6500, Description = "Escrow hold for Milestone: Action & Filtering Logic" }
        );

        // Expert Automation Transactions
        context.WalletTransactions.AddRange(
            new WalletTransaction { WalletId = walletAutomation.Id, UserId = expertAutomation.Id, PaymentId = paymentAuto1.Id, Type = WalletTransactionType.PAYMENT_RELEASE, Direction = TransactionDirection.CREDIT, Amount = 700, BalanceBefore = 0, BalanceAfter = 700, Description = "Payment release for Milestone: Script Development" },
            new WalletTransaction { WalletId = walletAutomation.Id, UserId = expertAutomation.Id, PaymentId = paymentAuto2.Id, Type = WalletTransactionType.PAYMENT_RELEASE, Direction = TransactionDirection.CREDIT, Amount = 800, BalanceBefore = 700, BalanceAfter = 1500, Description = "Payment release for Milestone: Deployment & Setup" },
            new WalletTransaction { WalletId = walletAutomation.Id, UserId = expertAutomation.Id, PaymentId = paymentAuto3.Id, Type = WalletTransactionType.PAYMENT_RELEASE, Direction = TransactionDirection.CREDIT, Amount = 1000, BalanceBefore = 1500, BalanceAfter = 2500, Description = "Payment release for Milestone: Zapier Trigger Integration" }
        );

        // Client Ecommerce Transactions
        context.WalletTransactions.AddRange(
            new WalletTransaction { WalletId = walletEcommerce.Id, UserId = clientEcommerce.Id, Type = WalletTransactionType.DEMO_DEPOSIT, Direction = TransactionDirection.CREDIT, Amount = 10000, BalanceBefore = 0, BalanceAfter = 10000, Description = "Initial demo deposit" },
            new WalletTransaction { WalletId = walletEcommerce.Id, UserId = clientEcommerce.Id, PaymentId = paymentOld1.Id, Type = WalletTransactionType.ESCROW_HOLD, Direction = TransactionDirection.DEBIT, Amount = 1500, BalanceBefore = 10000, BalanceAfter = 8500, Description = "Escrow hold for Milestone: Data Analysis & Model Design" },
            new WalletTransaction { WalletId = walletEcommerce.Id, UserId = clientEcommerce.Id, PaymentId = paymentOld2.Id, Type = WalletTransactionType.ESCROW_HOLD, Direction = TransactionDirection.DEBIT, Amount = 3000, BalanceBefore = 8500, BalanceAfter = 5500, Description = "Escrow hold for Milestone: Backend API Implementation" }
        );

        // Expert Senior AI Transaction
        context.WalletTransactions.Add(
            new WalletTransaction { WalletId = walletSeniorAI.Id, UserId = expertSeniorAI.Id, PaymentId = paymentOld1.Id, Type = WalletTransactionType.PAYMENT_RELEASE, Direction = TransactionDirection.CREDIT, Amount = 1500, BalanceBefore = 0, BalanceAfter = 1500, Description = "Payment release for Milestone: Data Analysis & Model Design" }
        );

        // Client Research Transactions
        context.WalletTransactions.AddRange(
            new WalletTransaction { WalletId = walletResearch.Id, UserId = clientResearch.Id, Type = WalletTransactionType.DEMO_DEPOSIT, Direction = TransactionDirection.CREDIT, Amount = 10000, BalanceBefore = 0, BalanceAfter = 10000, Description = "Initial demo deposit" },
            new WalletTransaction { WalletId = walletResearch.Id, UserId = clientResearch.Id, PaymentId = paymentOld3.Id, Type = WalletTransactionType.ESCROW_HOLD, Direction = TransactionDirection.DEBIT, Amount = 800, BalanceBefore = 10000, BalanceAfter = 9200, Description = "Escrow hold for Milestone: Full Analysis and Report" }
        );

        // Expert Data Scientist Transaction
        context.WalletTransactions.Add(
            new WalletTransaction { WalletId = walletDataScientist.Id, UserId = expertDataScientist.Id, PaymentId = paymentOld3.Id, Type = WalletTransactionType.PAYMENT_RELEASE, Direction = TransactionDirection.CREDIT, Amount = 800, BalanceBefore = 0, BalanceAfter = 800, Description = "Payment release for Milestone: Full Analysis and Report" }
        );
        await context.SaveChangesAsync();

        // ── 10. Communication (Conversations & Messages) ──────────────
        var conversation = new Conversation
        {
            ProjectId = projectAutoInProgress.Id,
            JobId = jobAutomationInProgress.Id,
            ClientId = clientStartup.Id,
            ExpertId = expertAutomation.Id
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        context.Messages.AddRange(
            new Message { ConversationId = conversation.Id, SenderId = clientStartup.Id, Content = "Hi Kenji, thanks for accepting the project. Let's start with the Zapier trigger integration.", IsRead = true, ReadAt = DateTime.UtcNow.AddDays(-4) },
            new Message { ConversationId = conversation.Id, SenderId = expertAutomation.Id, Content = "Hello! I will configure the webhook and test the data parsing first.", IsRead = true, ReadAt = DateTime.UtcNow.AddDays(-3) },
            new Message { ConversationId = conversation.Id, SenderId = expertAutomation.Id, Content = "I've completed the trigger integration and submitted the deliverable. Please review it.", IsRead = true, ReadAt = DateTime.UtcNow.AddDays(-1) },
            new Message { ConversationId = conversation.Id, SenderId = clientStartup.Id, Content = "Excellent work, I've approved the milestone and released the payment. Moving to the action logic.", IsRead = true, ReadAt = DateTime.UtcNow.AddDays(-1) }
        );

        // ── 11. Notifications ────────────────────────────────────────
        context.Notifications.AddRange(
            new Notification { UserId = clientStartup.Id, Title = "Deliverable Submitted", Message = "Expert Kenji Tanaka submitted a deliverable for milestone Action & Filtering Logic", Type = "MILESTONE", IsRead = false },
            new Notification { UserId = expertAutomation.Id, Title = "Milestone Funded", Message = "Client TechNova Solutions funded milestone Action & Filtering Logic", Type = "PAYMENT", IsRead = true }
        );
        await context.SaveChangesAsync();

        // ── 12. AI Job Suggestion & Recommendation ───────────────────
        var aiSuggestion = new AIJobSuggestion
        {
            ClientId = clientStartup.Id,
            RawInput = "I need to automate my leads from Facebook Lead Ads to HubSpot CRM",
            SuggestedTitle = "Facebook Lead Ads to HubSpot Automation Pipeline",
            SuggestedDescription = "Build a real-time lead sync system from Facebook Lead Ads webhook to HubSpot CRM contacts with duplicate check logic.",
            SuggestedBudgetMin = 500,
            SuggestedBudgetMax = 1500,
            SuggestedTimelineDays = 7,
            SuggestedExperienceLevel = SkillLevel.INTERMEDIATE,
            SuggestedBusinessDomain = "Marketing Automation",
            SuggestedExpectedOutcome = "Leads are automatically created in HubSpot CRM within 5 minutes of ad submit.",
            SuggestedSkillsJson = "[\"Zapier\", \"HubSpot API\", \"Webhooks\"]",
            SuggestedMilestonesJson = "[{\"Title\":\"Trigger Integration\", \"Amount\":400}, {\"Title\":\"HubSpot Sync & Deduplication\", \"Amount\":600}]",
            AIModel = "Gemini-1.5-Pro",
            Status = AIJobSuggestionStatus.GENERATED
        };
        context.AIJobSuggestions.Add(aiSuggestion);
        await context.SaveChangesAsync();

        context.RecommendationResults.Add(new RecommendationResult
        {
            JobId = jobAutomationInProgress.Id,
            ExpertId = expertAutomation.Id,
            TotalScore = 9.8m,
            SkillScore = 10.0m,
            PortfolioScore = 9.5m,
            RatingScore = 10.0m,
            BudgetScore = 9.5m,
            AvailabilityScore = 10.0m,
            CompletionScore = 9.8m,
            Explanation = "Kenji is a top match for your CRM Zapier automation job due to his strong portfolio in automation workflows."
        });
        await context.SaveChangesAsync();
    }
}
