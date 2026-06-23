using System;
using System.Collections.Generic;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;

namespace Aivora.Tests.ApiContract;

public static class ApiContractTestData
{
    public static readonly Guid ClientUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ExpertUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AdminUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static readonly Guid CategoryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid SkillId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid SkillId2 = Guid.Parse("55555555-5555-5555-5555-555555555556");

    public static void Seed(AivoraDbContext dbContext)
    {
        // Clear existing tables
        dbContext.Users.RemoveRange(dbContext.Users);
        dbContext.ClientProfiles.RemoveRange(dbContext.ClientProfiles);
        dbContext.ExpertProfiles.RemoveRange(dbContext.ExpertProfiles);
        dbContext.Categories.RemoveRange(dbContext.Categories);
        dbContext.Skills.RemoveRange(dbContext.Skills);
        dbContext.Wallets.RemoveRange(dbContext.Wallets);
        dbContext.JobPosts.RemoveRange(dbContext.JobPosts);
        dbContext.Proposals.RemoveRange(dbContext.Proposals);
        dbContext.Projects.RemoveRange(dbContext.Projects);
        dbContext.Milestones.RemoveRange(dbContext.Milestones);
        dbContext.Deliverables.RemoveRange(dbContext.Deliverables);
        dbContext.Payments.RemoveRange(dbContext.Payments);
        dbContext.WalletTransactions.RemoveRange(dbContext.WalletTransactions);
        dbContext.Conversations.RemoveRange(dbContext.Conversations);
        dbContext.Messages.RemoveRange(dbContext.Messages);
        dbContext.Notifications.RemoveRange(dbContext.Notifications);
        dbContext.Reviews.RemoveRange(dbContext.Reviews);
        dbContext.Disputes.RemoveRange(dbContext.Disputes);
        dbContext.DisputeEvidences.RemoveRange(dbContext.DisputeEvidences);
        dbContext.ExpertSkills.RemoveRange(dbContext.ExpertSkills);
        dbContext.JobSkills.RemoveRange(dbContext.JobSkills);
        dbContext.RecommendationResults.RemoveRange(dbContext.RecommendationResults);
        dbContext.AIJobSuggestions.RemoveRange(dbContext.AIJobSuggestions);
        dbContext.SaveChanges();

        // Add Category & Skills
        var category = new Category
        {
            Id = CategoryId,
            Name = "Web Development",
            Description = "Custom web development services",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Categories.Add(category);

        var skill = new Skill
        {
            Id = SkillId,
            Name = "React",
            CategoryId = CategoryId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Skills.Add(skill);

        var skill2 = new Skill
        {
            Id = SkillId2,
            Name = "Node.js",
            CategoryId = CategoryId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Skills.Add(skill2);

        // Add Users
        // Hash password123 using BCrypt
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("password123");

        var client = new User
        {
            Id = ClientUserId,
            Email = "client@aivora.com",
            PasswordHash = passwordHash,
            FullName = "Aivora Client",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(client);

        var expert = new User
        {
            Id = ExpertUserId,
            Email = "expert@aivora.com",
            PasswordHash = passwordHash,
            FullName = "Aivora Expert",
            Role = UserRole.EXPERT,
            Status = UserStatus.ACTIVE,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(expert);

        var admin = new User
        {
            Id = AdminUserId,
            Email = "admin@aivora.com",
            PasswordHash = passwordHash,
            FullName = "Aivora Admin",
            Role = UserRole.ADMIN,
            Status = UserStatus.ACTIVE,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(admin);

        // Add Profiles
        var clientProfile = new ClientProfile
        {
            UserId = ClientUserId,
            CompanyName = "Aivora Client Inc.",
            Rating = 0,
            TotalReviews = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ClientProfiles.Add(clientProfile);

        var expertProfile = new ExpertProfile
        {
            UserId = ExpertUserId,
            Title = "Full Stack Engineer",
            ExperienceYears = 5,
            Rating = 0,
            TotalReviews = 0,
            CompletedProjects = 0,
            SuccessRate = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ExpertProfiles.Add(expertProfile);

        // Add Wallets
        var clientWallet = new Wallet
        {
            UserId = ClientUserId,
            AvailableBalance = 10000,
            HeldBalance = 0,
            Currency = "AICOIN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Wallets.Add(clientWallet);

        var expertWallet = new Wallet
        {
            UserId = ExpertUserId,
            AvailableBalance = 0,
            HeldBalance = 0,
            Currency = "AICOIN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Wallets.Add(expertWallet);

        dbContext.SaveChanges();
    }
}
