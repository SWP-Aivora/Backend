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
            Console.WriteLine($"Warning: Duplicate key violation during seeding - {pgEx.Message}");
            context.ChangeTracker.Clear();
        }
    }

    public static async Task Initialize(AivoraDbContext context, bool forceReset = false)
    {
        context.ChangeTracker.Clear();

        if (forceReset)
        {
            Console.WriteLine("SeedForceReset=true: Resetting database and seeding fresh data...");
            await ResetDatabase(context);
            await SeedDatabase(context);
        }
        else
        {
            var userCount = await context.Users.AsNoTracking().CountAsync();
            if (userCount == 0)
            {
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
        // Delete in reverse order of foreign key dependencies
        context.WalletTransactions.RemoveRange(context.WalletTransactions);
        context.Payments.RemoveRange(context.Payments);
        context.Notifications.RemoveRange(context.Notifications);
        context.Messages.RemoveRange(context.Messages);
        context.Conversations.RemoveRange(context.Conversations);
        context.Reviews.RemoveRange(context.Reviews);
        context.DisputeEvidences.RemoveRange(context.DisputeEvidences);
        context.Disputes.RemoveRange(context.Disputes);

        context.Deliverables.RemoveRange(context.Deliverables);
        context.Milestones.RemoveRange(context.Milestones);
        context.Projects.RemoveRange(context.Projects);
        context.ProposalMilestones.RemoveRange(context.ProposalMilestones);
        context.Proposals.RemoveRange(context.Proposals);

        context.JobPostMilestones.RemoveRange(context.JobPostMilestones);
        context.JobSkills.RemoveRange(context.JobSkills);
        context.AIJobSuggestions.RemoveRange(context.AIJobSuggestions);
        context.JobPosts.RemoveRange(context.JobPosts);
        context.RecommendationResults.RemoveRange(context.RecommendationResults);

        context.ExpertSkills.RemoveRange(context.ExpertSkills);
        context.ClientProfiles.RemoveRange(context.ClientProfiles);
        context.ExpertProfiles.RemoveRange(context.ExpertProfiles);
        context.Skills.RemoveRange(context.Skills);
        context.Categories.RemoveRange(context.Categories);

        context.Wallets.RemoveRange(context.Wallets);
        context.Users.RemoveRange(context.Users);

        await SaveChangesWithDuplicateHandling(context);
    }

    private static async Task SeedDatabase(AivoraDbContext context)
    {
        try
        {
            // Create users
            var users = new List<User>
            {
                new User { Email = "admin@aivora.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Platform Admin", Role = UserRole.ADMIN, Status = UserStatus.ACTIVE },
                new User { Email = "Ahihi@aivora.com", PasswordHash = BCryptNet.HashPassword("Ahihi123"), FullName = "Ahihi Admin", Role = UserRole.ADMIN, Status = UserStatus.ACTIVE },
                new User { Email = "client.startup@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "TechNova Solutions", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE },
                new User { Email = "client.ecommerce@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Glamour Boutique", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE },
                new User { Email = "client.research@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "John Doe", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE },
                new User { Email = "expert.senior.ai@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Dr. Evelyn Reed", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE },
                new User { Email = "expert.fullstack@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Marcus Chen", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE },
                new User { Email = "expert.data.scientist@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Isabella Rossi", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE },
                new User { Email = "expert.automation@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Kenji Tanaka", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE },
                new User { Email = "expert.junior.ai@demo.com", PasswordHash = BCryptNet.HashPassword("123456"), FullName = "Ben Carter", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE }
            };

            // Setup wallets
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

            // Insert users
            context.Users.AddRange(users);
            await SaveChangesWithDuplicateHandling(context);

            // Get user IDs
            var userIds = await context.Users
                .Where(u => users.Select(u2 => u2.Email).Contains(u.Email))
                .ToDictionaryAsync(u => u.Email, u => u.Id);

            // Create profiles
            var clientProfiles = new List<ClientProfile>
            {
                new ClientProfile { UserId = userIds["client.startup@demo.com"], CompanyName = "TechNova Solutions" },
                new ClientProfile { UserId = userIds["client.ecommerce@demo.com"], CompanyName = "Glamour Boutique" },
                new ClientProfile { UserId = userIds["client.research@demo.com"], CompanyName = "Independent Researcher" }
            };

            var expertProfiles = new List<ExpertProfile>
            {
                new ExpertProfile { UserId = userIds["expert.senior.ai@demo.com"], Title = "Principal AI Engineer", Bio = "10+ years in ML.", HourlyRate = 150 },
                new ExpertProfile { UserId = userIds["expert.fullstack@demo.com"], Title = "Full-Stack Developer | AI Integrator", Bio = "Building scalable web apps with AI.", HourlyRate = 90 },
                new ExpertProfile { UserId = userIds["expert.data.scientist@demo.com"], Title = "Data Scientist", Bio = "Turning data into insights.", HourlyRate = 120 },
                new ExpertProfile { UserId = userIds["expert.automation@demo.com"], Title = "Automation Specialist", Bio = "Automating business processes.", HourlyRate = 80 },
                new ExpertProfile { UserId = userIds["expert.junior.ai@demo.com"], Title = "AI Developer", Bio = "Eager to build great AI products.", HourlyRate = 50 }
            };

            context.ClientProfiles.AddRange(clientProfiles);
            context.ExpertProfiles.AddRange(expertProfiles);
            await SaveChangesWithDuplicateHandling(context);

            // Create categories
            var categories = new List<Category>
            {
                new Category { Name = "Software Development", Description = "Custom software development" },
                new Category { Name = "AI/ML", Description = "Machine learning and AI solutions" },
                new Category { Name = "Data Science", Description = "Data analysis and analytics" },
                new Category { Name = "Design", Description = "UI/UX and graphic design" }
            };

            context.Categories.AddRange(categories);
            await SaveChangesWithDuplicateHandling(context);

            // Create skills
            var skills = new List<Skill>
            {
                new Skill { Name = "C#", CategoryId = categories[0].Id },
                new Skill { Name = ".NET Core", CategoryId = categories[0].Id },
                new Skill { Name = "JavaScript", CategoryId = categories[0].Id },
                new Skill { Name = "React", CategoryId = categories[0].Id },
                new Skill { Name = "Python", CategoryId = categories[1].Id },
                new Skill { Name = "Machine Learning", CategoryId = categories[1].Id },
                new Skill { Name = "Data Analysis", CategoryId = categories[2].Id },
                new Skill { Name = "UI/UX Design", CategoryId = categories[3].Id }
            };

            context.Categories.AddRange(categories);
            context.Skills.AddRange(skills);
            await SaveChangesWithDuplicateHandling(context);

            // Create job posts
            var jobChatbot = new JobPost
            {
                ClientId = userIds["client.startup@demo.com"],
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

            // Create proposals
            var proposals = new List<Proposal>
            {
                new Proposal { JobId = jobChatbot.Id, ExpertId = userIds["expert.senior.ai@demo.com"], CoverLetter = "I have extensive experience building chatbots with RAG and LangChain.", ProposedBudget = 4500 },
                new Proposal { JobId = jobChatbot.Id, ExpertId = userIds["expert.fullstack@demo.com"], CoverLetter = "I can build and integrate this chatbot into your existing platform.", ProposedBudget = 3000 }
            };

            context.Proposals.AddRange(proposals);
            await SaveChangesWithDuplicateHandling(context);

            Console.WriteLine("Demo data seeded successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during seeding: {ex.Message}");
            throw;
        }
    }
}