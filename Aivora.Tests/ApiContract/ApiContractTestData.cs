using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;

namespace Aivora.Tests.ApiContract;

public static class ApiContractTestData
{
    // ── Fixed GUIDs ──────────────────────────────────────────────
    public static readonly Guid ClientUserId = new("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid ExpertUserId = new("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid AdminUserId = new("a0000000-0000-0000-0000-000000000003");

    public static readonly Guid CategoryId = new("a0000000-0000-0000-0000-000000000010");
    public static readonly Guid SkillAiId = new("a0000000-0000-0000-0000-000000000011");
    public static readonly Guid SkillWebId = new("a0000000-0000-0000-0000-000000000012");

    public static readonly Guid ClientWalletId = new("a0000000-0000-0000-0000-000000000020");
    public static readonly Guid ExpertWalletId = new("a0000000-0000-0000-0000-000000000021");
    public static readonly Guid AdminWalletId = new("a0000000-0000-0000-0000-000000000022");

    // ── Test password hash (not a real hash — only for in‑memory tests) ──
    public const string TestPasswordHash = "$2a$11$TEST_HASH_NOT_REAL_DO_NOT_USE_IN_PROD";

    // ── Seed ──────────────────────────────────────────────────────
    public static void Seed(AivoraDbContext db)
    {
        if (db.Users.Any()) return; // already seeded

        // Users
        db.Users.AddRange(
            new User
            {
                Id = ClientUserId,
                Email = "client@aivora.test",
                PasswordHash = TestPasswordHash,
                FullName = "Test Client",
                Role = UserRole.CLIENT,
                Status = UserStatus.ACTIVE
            },
            new User
            {
                Id = ExpertUserId,
                Email = "expert@aivora.test",
                PasswordHash = TestPasswordHash,
                FullName = "Test Expert",
                Role = UserRole.EXPERT,
                Status = UserStatus.ACTIVE
            },
            new User
            {
                Id = AdminUserId,
                Email = "admin@aivora.test",
                PasswordHash = TestPasswordHash,
                FullName = "Test Admin",
                Role = UserRole.ADMIN,
                Status = UserStatus.ACTIVE
            }
        );

        // Category
        db.Categories.Add(new Category
        {
            Id = CategoryId,
            Name = "AI & Machine Learning",
            Description = "Artificial intelligence and machine learning projects"
        });

        // Skills
        db.Skills.AddRange(
            new Skill { Id = SkillAiId, Name = "Machine Learning", CategoryId = CategoryId },
            new Skill { Id = SkillWebId, Name = "Web Development", CategoryId = CategoryId }
        );

        // Wallets
        db.Wallets.AddRange(
            new Wallet { Id = ClientWalletId, UserId = ClientUserId, AvailableBalance = 10000, Currency = "AICOIN" },
            new Wallet { Id = ExpertWalletId, UserId = ExpertUserId, AvailableBalance = 0, Currency = "AICOIN" },
            new Wallet { Id = AdminWalletId, UserId = AdminUserId, AvailableBalance = 0, Currency = "AICOIN" }
        );

        db.SaveChanges();
    }
}
