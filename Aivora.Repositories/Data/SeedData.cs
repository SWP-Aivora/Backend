using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Microsoft.EntityFrameworkCore;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Aivora.Repositories.Data;

public static class SeedData
{
    private static async Task SaveChangesWithDuplicateHandling(AivoraDbContext context)
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Handle duplicate key constraint violation gracefully
            // This can happen when seeding runs multiple times or when data already exists
            Console.WriteLine($"Warning: Duplicate key violation during seeding - {pgEx.Message}");
            // Clear tracking state to avoid issues with subsequent queries
            context.ChangeTracker.Clear();
        }
    }

    public static async Task Initialize(AivoraDbContext context, bool forceReset = false)
    {
        // Always clear tracking state to avoid stale references
        context.ChangeTracker.Clear();

        if (forceReset)
        {
            // Delete all data and seed fresh database
            Console.WriteLine("SeedForceReset=true: Resetting database and seeding fresh data...");
            await ResetDatabase(context);
            await SeedDatabase(context);
        }
        else
        {
            // Check if database is empty
            var userCount = await context.Users.AsNoTracking().CountAsync();
            if (userCount == 0)
            {
                // Only seed when database is empty
                Console.WriteLine("Database is empty - seeding initial data...");
                await SeedDatabase(context);
            }
            else
            {
                Console.WriteLine("Database already contains data - skipping seeding.");
            }
        }
    }

    private static async Task ResetDatabase(AivoraDbContext context)
    {
        // Finance & Communication (Leaf nodes) - phải xóa trước do foreign key constraints
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

        // Profiles & Taxonomy - phải xóa profiles trước do foreign key constraints
        context.ExpertSkills.RemoveRange(context.ExpertSkills);
        context.ClientProfiles.RemoveRange(context.ClientProfiles);
        context.ExpertProfiles.RemoveRange(context.ExpertProfiles);
        context.Skills.RemoveRange(context.Skills);
        context.Categories.RemoveRange(context.Categories);

        // Identity (Root nodes) - Wallets phụ thuộc vào Users
        context.Wallets.RemoveRange(context.Wallets);
        context.Users.RemoveRange(context.Users);

        await SaveChangesWithDuplicateHandling(context);
    }

    private static async Task SeedDatabase(AivoraDbContext context)
    {
        try
        {
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

            var users = new List<User> { admin1, admin2, clientStartup, clientEcommerce, clientResearch, expertSeniorAI, expertFullstack, expertDataScientist, expertAutomation, expertJuniorAI };

            // Setup wallets for all users (database is empty when forceReset=true)
            foreach (var user in users)
            {
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
                    AvailableBalance = available,
                    HeldBalance = held,
                    TotalEarned = earned,
                    Currency = "AICOIN"
                };
            }

            // Insert all users
            context.Users.AddRange(users);
            await SaveChangesWithDuplicateHandling(context);

            // Query user IDs after insert
            var userIds = await context.Users
                .Where(u => users.Select(u2 => u2.Email).Contains(u.Email))
                .ToDictionaryAsync(u => u.Email, u => u.Id);

            // ── 2. Profiles ──────────────────────────────────────────────
            context.ClientProfiles.AddRange(
                new ClientProfile { UserId = userIds[clientStartup.Email], CompanyName = "TechNova Solutions" },
                new ClientProfile { UserId = userIds[clientEcommerce.Email], CompanyName = "Glamour Boutique" },
                new ClientProfile { UserId = userIds[clientResearch.Email], CompanyName = "Independent Researcher" }
            );
            context.ExpertProfiles.AddRange(
                new ExpertProfile { UserId = userIds[expertSeniorAI.Email], Title = "Principal AI Engineer", Bio = "10+ years in ML.", HourlyRate = 150 },
                new ExpertProfile { UserId = userIds[expertFullstack.Email], Title = "Full-Stack Developer | AI Integrator", Bio = "Building scalable web apps with AI.", HourlyRate = 90 },
                new ExpertProfile { UserId = userIds[expertDataScientist.Email], Title = "Data Scientist", Bio = "Turning data into insights.", HourlyRate = 120 },
                new ExpertProfile { UserId = userIds[expertAutomation.Email], Title = "Automation Specialist", Bio = "Automating business processes.", HourlyRate = 80 },
                new ExpertProfile { UserId = userIds[expertJuniorAI.Email], Title = "AI Developer", Bio = "Eager to build great AI products.", HourlyRate = 50 }
            );
            await SaveChangesWithDuplicateHandling(context);

            // Continue with other seed data (categories, skills, etc.)
            await SeedAdditionalData(context, userIds);

            // Seed all demo data (projects, jobs, payments, etc.)
            await SeedDemoData(context, userIds);
        }

        private static async Task SeedAdditionalData(AivoraDbContext context, Dictionary<string, Guid> userIds)
        {
            // ── 3. Categories & Skills ─────────────────────────────────────
            var devCategory = new Category { Name = "Software Development", Description = "Custom software development" };
            var aiCategory = new Category { Name = "AI/ML", Description = "Machine learning and AI solutions" };
            var dataCategory = new Category { Name = "Data Science", Description = "Data analysis and analytics" };
            var designCategory = new Category { Name = "Design", Description = "UI/UX and graphic design" };

            context.Categories.AddRange(devCategory, aiCategory, dataCategory, designCategory);
            await SaveChangesWithDuplicateHandling(context);

            var skills = new List<Skill>
            {
                new Skill { Name = "C#", Level = SkillLevel.EXPERT },
                new Skill { Name = ".NET Core", Level = SkillLevel.EXPERT },
                new Skill { Name = "JavaScript", Level = SkillLevel.INTERMEDIATE },
                new Skill { Name = "React", Level = SkillLevel.INTERMEDIATE },
                new Skill { Name = "Python", Level = SkillLevel.EXPERT },
                new Skill { Name = "Machine Learning", Level = SkillLevel.EXPERT },
                new Skill { Name = "Data Analysis", Level = SkillLevel.INTERMEDIATE },
                new Skill { Name = "UI/UX Design", Level = SkillLevel.INTERMEDIATE }
            };
            context.Skills.AddRange(skills);
            await SaveChangesWithDuplicateHandling(context);

            // ── 4. User-Skills Mapping ───────────────────────────────────
            // Expert skills mapping
            context.ExpertSkills.AddRange(
                new ExpertSkill { ExpertId = userIds["expert.senior.ai@demo.com"], SkillId = skills.Find(s => s.Name == "Python").Id, Verified = true },
                new ExpertSkill { ExpertId = userIds["expert.senior.ai@demo.com"], SkillId = skills.Find(s => s.Name == "Machine Learning").Id, Verified = true },
                new ExpertSkill { ExpertId = userIds["expert.fullstack@demo.com"], SkillId = skills.Find(s => s.Name == "C#").Id, Verified = true },
                new ExpertSkill { ExpertId = userIds["expert.fullstack@demo.com"], SkillId = skills.Find(s => s.Name == ".NET Core").Id, Verified = true },
                new ExpertSkill { ExpertId = userIds["expert.fullstack@demo.com"], SkillId = skills.Find(s => s.Name == "JavaScript").Id, Verified = true },
                new ExpertSkill { ExpertId = userIds["expert.data.scientist@demo.com"], SkillId = skills.Find(s => s.Name == "Python").Id, Verified = true },
                new ExpertSkill { ExpertId = userIds["expert.data.scientist@demo.com"], SkillId = skills.Find(s => s.Name == "Data Analysis").Id, Verified = true },
                new ExpertSkill { ExpertId = userIds["expert.automation@demo.com"], SkillId = skills.Find(s => s.Name == "JavaScript").Id, Verified = true },
                new ExpertSkill { ExpertId = userIds["expert.junior.ai@demo.com"], SkillId = skills.Find(s => s.Name == "Python").Id, Verified = false }
            );
            await SaveChangesWithDuplicateHandling(context);
        }


    private static async Task SeedDemoData(AivoraDbContext context, Dictionary<string, Guid> userIds)
    {
        // Get user references
        var clientStartup = context.Users.First(u => u.Email == "client.startup@demo.com");
        var clientEcommerce = context.Users.First(u => u.Email == "client.ecommerce@demo.com");
        var clientResearch = context.Users.First(u => u.Email == "client.research@demo.com");
        var expertSeniorAI = context.Users.First(u => u.Email == "expert.senior.ai@demo.com");
        var expertFullstack = context.Users.First(u => u.Email == "expert.fullstack@demo.com");
        var expertDataScientist = context.Users.First(u => u.Email == "expert.data.scientist@demo.com");
        var expertAutomation = context.Users.First(u => u.Email == "expert.automation@demo.com");

        // ── 1. Jobs & Proposals ───────────────────────────────────────
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
        await SaveChangesWithDuplicateHandling(context);

        context.Proposals.AddRange(
            new Proposal { JobId = jobChatbot.Id, ExpertId = expertSeniorAI.Id, CoverLetter = "I have extensive experience building chatbots with RAG and LangChain.", ProposedBudget = 4500 },
            new Proposal { JobId = jobChatbot.Id, ExpertId = expertFullstack.Id, CoverLetter = "I can build and integrate this chatbot into your existing platform.", ProposedBudget = 3000 }
        );
        await SaveChangesWithDuplicateHandling(context);

        Console.WriteLine("Demo data seeded successfully!");
    }
}
