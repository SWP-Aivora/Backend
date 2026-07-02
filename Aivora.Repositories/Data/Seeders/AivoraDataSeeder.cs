using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Repositories;

public class AivoraDataSeeder : IAivoraDataSeeder
{
    private readonly AivoraDbContext _context;

    public AivoraDataSeeder(AivoraDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        await _context.Database.EnsureCreatedAsync();

        if (!await _context.Users.AnyAsync())
        {
            await SeedDefaultData();
        }
    }

    private async Task SeedDefaultData()
    {
        // Hash passwords
        var adminPasswordHash = HashPassword("123456");
        var admin2PasswordHash = HashPassword("ahihi123"); // Keep specific password for ahihi admin
        var clientPasswordHash = HashPassword("123456");
        var expertPasswordHash = HashPassword("123456");

        // Seed Categories and Skills
        var categories = await SeedCategoriesAndSkills();

        // Seed Users
        var users = new List<User>
        {
            // Admin 1 (Default)
            new User
            {
                Email = "admin@aivora.com",
                PasswordHash = adminPasswordHash,
                FullName = "Main Admin",
                Role = UserRole.ADMIN,
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },

            // Admin 2 (New as requested)
            new User
            {
                Email = "ahihi@aivora.com",
                PasswordHash = admin2PasswordHash,
                FullName = "Ahihi Admin",
                Role = UserRole.ADMIN,
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },

            // Clients
            new User
            {
                Email = "client1@example.com",
                PasswordHash = clientPasswordHash,
                FullName = "Client One",
                Role = UserRole.CLIENT,
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },

            new User
            {
                Email = "client2@example.com",
                PasswordHash = clientPasswordHash,
                FullName = "Client Two",
                Role = UserRole.CLIENT,
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },

            // Experts
            new User
            {
                Email = "expert1@example.com",
                PasswordHash = expertPasswordHash,
                FullName = "Expert One",
                Role = UserRole.EXPERT,
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },

            new User
            {
                Email = "expert2@example.com",
                PasswordHash = expertPasswordHash,
                FullName = "Expert Two",
                Role = UserRole.EXPERT,
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },

            new User
            {
                Email = "expert3@example.com",
                PasswordHash = expertPasswordHash,
                FullName = "Expert Three",
                Role = UserRole.EXPERT,
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },

            new User
            {
                Email = "expert4@example.com",
                PasswordHash = expertPasswordHash,
                FullName = "Expert Four",
                Role = UserRole.EXPERT,
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        // Seed Profiles
        await SeedProfiles(users);

        // Seed Job Posts
        await SeedJobPosts(users, categories);

        // Seed Projects and related data
        await SeedProjectsAndRelated(users);
    }

    private async Task<List<Category>> SeedCategoriesAndSkills()
    {
        var categories = new List<Category>
        {
            new Category
            {
                Name = "Web Development",
                Description = "Website and web application development",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Category
            {
                Name = "Mobile Development",
                Description = "Mobile app development for iOS and Android",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Category
            {
                Name = "AI/ML",
                Description = "Artificial Intelligence and Machine Learning",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Category
            {
                Name = "Design",
                Description = "UI/UX and Graphic Design",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Category
            {
                Name = "Marketing",
                Description = "Digital marketing and content creation",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        await _context.Categories.AddRangeAsync(categories);
        await _context.SaveChangesAsync();

        // Seed Skills
        var skills = new List<Skill>();

        // Web Development Skills
        var webDevSkills = new[]
        {
            "Frontend Development", "Backend Development", "Database Design",
            "API Development", "DevOps", "Testing", "Performance Optimization"
        };

        // Mobile Development Skills
        var mobileSkills = new[]
        {
            "iOS Development", "Android Development", "React Native", "Flutter",
            "Mobile UI Design", "Mobile API Integration", "App Store Publishing"
        };

        // AI/ML Skills
        var aiSkills = new[]
        {
            "Machine Learning", "Deep Learning", "NLP", "Computer Vision",
            "Data Science", "MLOps", "AI Model Deployment"
        };

        // Design Skills
        var designSkills = new[]
        {
            "UI Design", "UX Design", "Graphic Design", "Logo Design",
            "Prototyping", "User Research", "Design Systems"
        };

        // Marketing Skills
        var marketingSkills = new[]
        {
            "SEO", "Content Marketing", "Social Media Marketing", "PPC Advertising",
            "Email Marketing", "Analytics", "Brand Strategy"
        };

        // Add skills to categories
        foreach (var skillName in webDevSkills)
        {
            skills.Add(new Skill
            {
                Name = skillName,
                CategoryId = categories[0].Id, // Web Development
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        foreach (var skillName in mobileSkills)
        {
            skills.Add(new Skill
            {
                Name = skillName,
                CategoryId = categories[1].Id, // Mobile Development
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        foreach (var skillName in aiSkills)
        {
            skills.Add(new Skill
            {
                Name = skillName,
                CategoryId = categories[2].Id, // AI/ML
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        foreach (var skillName in designSkills)
        {
            skills.Add(new Skill
            {
                Name = skillName,
                CategoryId = categories[3].Id, // Design
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        foreach (var skillName in marketingSkills)
        {
            skills.Add(new Skill
            {
                Name = skillName,
                CategoryId = categories[4].Id, // Marketing
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await _context.Skills.AddRangeAsync(skills);
        await _context.SaveChangesAsync();

        return categories;
    }

    private async Task SeedProfiles(List<User> users)
    {
        // Client Profiles
        var client1Profile = new ClientProfile
        {
            UserId = users.FirstOrDefault(u => u.Email == "client1@example.com")!.Id,
            CompanyName = "Tech Corp",
            Industry = "Technology",
            CompanySize = "50-100",
            Description = "Leading technology company specializing in innovative solutions",
            Rating = 4.5M,
            TotalReviews = 10,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var client2Profile = new ClientProfile
        {
            UserId = users.FirstOrDefault(u => u.Email == "client2@example.com")!.Id,
            CompanyName = "StartupXYZ",
            Industry = "E-commerce",
            CompanySize = "1-10",
            Description = "Fast-growing e-commerce startup",
            Rating = 4.2M,
            TotalReviews = 5,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _context.ClientProfiles.AddRangeAsync(new[] { client1Profile, client2Profile });
        await _context.SaveChangesAsync();

        // Expert Profiles
        var expert1Profile = new ExpertProfile
        {
            UserId = users.FirstOrDefault(u => u.Email == "expert1@example.com")!.Id,
            Title = "Senior Full-stack Developer",
            Bio = "5+ years experience in web development with expertise in React, Node.js, and PostgreSQL. Passionate about building scalable and user-friendly applications.",
            HourlyRate = 50M,
            ExperienceYears = 5,
            AvailabilityStatus = AvailabilityStatus.AVAILABLE,
            Rating = 4.8M,
            TotalReviews = 20,
            CompletedProjects = 15,
            SuccessRate = 95,
            ResponseTimeMinutes = 30,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var expert2Profile = new ExpertProfile
        {
            UserId = users.FirstOrDefault(u => u.Email == "expert2@example.com")!.Id,
            Title = "UI/UX Designer",
            Bio = "Creative designer with 3 years experience in creating beautiful and functional user interfaces. Specialized in Figma and Adobe Creative Suite.",
            HourlyRate = 40M,
            ExperienceYears = 3,
            AvailabilityStatus = AvailabilityStatus.AVAILABLE,
            Rating = 4.5M,
            TotalReviews = 15,
            CompletedProjects = 10,
            SuccessRate = 90,
            ResponseTimeMinutes = 45,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var expert3Profile = new ExpertProfile
        {
            UserId = users.FirstOrDefault(u => u.Email == "expert3@example.com")!.Id,
            Title = "Mobile App Developer",
            Bio = "Cross-platform mobile developer with expertise in React Native and Flutter. Built 20+ mobile apps for various industries.",
            HourlyRate = 60M,
            ExperienceYears = 4,
            AvailabilityStatus = AvailabilityStatus.AVAILABLE,
            Rating = 4.7M,
            TotalReviews = 18,
            CompletedProjects = 12,
            SuccessRate = 92,
            ResponseTimeMinutes = 25,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var expert4Profile = new ExpertProfile
        {
            UserId = users.FirstOrDefault(u => u.Email == "expert4@example.com")!.Id,
            Title = "ML Engineer",
            Bio = "Machine Learning engineer with PhD in AI. Specialized in computer vision and natural language processing.",
            HourlyRate = 80M,
            ExperienceYears = 6,
            AvailabilityStatus = AvailabilityStatus.AVAILABLE,
            Rating = 4.9M,
            TotalReviews = 25,
            CompletedProjects = 18,
            SuccessRate = 96,
            ResponseTimeMinutes = 60,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _context.ExpertProfiles.AddRangeAsync(new[] { expert1Profile, expert2Profile, expert3Profile, expert4Profile });
        await _context.SaveChangesAsync();

        // Seed Expert Skills
        var expertSkills = new List<ExpertSkill>();

        // Expert 1 Skills (Web Development)
        var expert1Skills = new[] { "Frontend Development", "Backend Development", "Database Design", "API Development" };
        foreach (var skillName in expert1Skills)
        {
            var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name == skillName);
            if (skill != null)
            {
                expertSkills.Add(new ExpertSkill
                {
                    ExpertId = expert1Profile.Id,
                    SkillId = skill.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Expert 2 Skills (Design)
        var expert2Skills = new[] { "UI Design", "UX Design", "Prototyping", "Graphic Design" };
        foreach (var skillName in expert2Skills)
        {
            var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name == skillName);
            if (skill != null)
            {
                expertSkills.Add(new ExpertSkill
                {
                    ExpertId = expert2Profile.Id,
                    SkillId = skill.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Expert 3 Skills (Mobile)
        var expert3Skills = new[] { "React Native", "Flutter", "Mobile UI Design", "Mobile API Integration" };
        foreach (var skillName in expert3Skills)
        {
            var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name == skillName);
            if (skill != null)
            {
                expertSkills.Add(new ExpertSkill
                {
                    ExpertId = expert3Profile.Id,
                    SkillId = skill.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Expert 4 Skills (AI/ML)
        var expert4Skills = new[] { "Machine Learning", "Deep Learning", "Computer Vision", "NLP" };
        foreach (var skillName in expert4Skills)
        {
            var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name == skillName);
            if (skill != null)
            {
                expertSkills.Add(new ExpertSkill
                {
                    ExpertId = expert4Profile.Id,
                    SkillId = skill.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await _context.ExpertSkills.AddRangeAsync(expertSkills);
        await _context.SaveChangesAsync();
    }

    private async Task SeedJobPosts(List<User> users, List<Category> categories)
    {
        // Job Post 1 - Web Development
        var jobPost1 = new JobPost
        {
            ClientId = users.FirstOrDefault(u => u.Email == "client1@example.com")!.Id,
            CategoryId = categories[0].Id, // Web Development
            Title = "Build E-commerce Website",
            OriginalDescription = "We need a modern e-commerce website with React and Node.js. The site should include user authentication, product catalog, shopping cart, payment integration, and admin panel. Experience with PostgreSQL database is required.",
            BusinessDomain = "E-commerce",
            ExpectedOutcome = "A fully functional e-commerce platform ready for production deployment",
            BudgetType = BudgetType.FIXED,
            BudgetMin = 5000,
            BudgetMax = 8000,
            TimelineDays = 30,
            Status = JobStatus.OPEN,
            Visibility = JobVisibility.PUBLIC,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-5),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Job Post 2 - Mobile Development
        var jobPost2 = new JobPost
        {
            ClientId = users.FirstOrDefault(u => u.Email == "client2@example.com")!.Id,
            CategoryId = categories[1].Id, // Mobile Development
            Title = "Mobile App for Fitness Tracking",
            OriginalDescription = "Cross-platform mobile app for iOS and Android that tracks fitness activities, workouts, and nutrition. The app should include user profiles, goal setting, progress tracking, and social features.",
            BusinessDomain = "Health & Fitness",
            ExpectedOutcome = "A complete fitness tracking app with all core features",
            BudgetType = BudgetType.FIXED,
            BudgetMin = 10000,
            BudgetMax = 15000,
            TimelineDays = 45,
            Status = JobStatus.OPEN,
            Visibility = JobVisibility.PUBLIC,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-3),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Job Post 3 - UI/UX Design
        var jobPost3 = new JobPost
        {
            ClientId = users.FirstOrDefault(u => u.Email == "client1@example.com")!.Id,
            CategoryId = categories[3].Id, // Design
            Title = "Redesign Company Website UI",
            OriginalDescription = "Need to redesign the UI/UX for our company website. The current design is outdated and needs a modern, professional look. The redesign should include wireframes, mockups, and design system.",
            BusinessDomain = "Technology",
            ExpectedOutcome = "Complete UI/UX redesign with design system and assets",
            BudgetType = BudgetType.FIXED,
            BudgetMin = 3000,
            BudgetMax = 5000,
            TimelineDays = 20,
            Status = JobStatus.OPEN,
            Visibility = JobVisibility.PUBLIC,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _context.JobPosts.AddRangeAsync(new[] { jobPost1, jobPost2, jobPost3 });
        await _context.SaveChangesAsync();

        // Seed Job Skills
        var jobSkills = new List<JobSkill>();

        // Job Post 1 Skills
        var job1Skills = new[] { "Frontend Development", "Backend Development", "Database Design" };
        foreach (var skillName in job1Skills)
        {
            var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name == skillName);
            if (skill != null)
            {
                jobSkills.Add(new JobSkill
                {
                    JobId = jobPost1.Id,
                    SkillId = skill.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Job Post 2 Skills
        var job2Skills = new[] { "React Native", "Mobile UI Design", "Mobile API Integration" };
        foreach (var skillName in job2Skills)
        {
            var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name == skillName);
            if (skill != null)
            {
                jobSkills.Add(new JobSkill
                {
                    JobId = jobPost2.Id,
                    SkillId = skill.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Job Post 3 Skills
        var job3Skills = new[] { "UI Design", "UX Design", "Prototyping" };
        foreach (var skillName in job3Skills)
        {
            var skill = await _context.Skills.FirstOrDefaultAsync(s => s.Name == skillName);
            if (skill != null)
            {
                jobSkills.Add(new JobSkill
                {
                    JobId = jobPost3.Id,
                    SkillId = skill.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await _context.JobSkills.AddRangeAsync(jobSkills);
        await _context.SaveChangesAsync();
    }

    private async Task SeedProjectsAndRelated(List<User> users)
    {
        // Get Job Posts
        var jobPost1 = await _context.JobPosts
            .Include(j => j.Client)
            .FirstOrDefaultAsync(j => j.Title == "Build E-commerce Website");

        var jobPost2 = await _context.JobPosts
            .Include(j => j.Client)
            .FirstOrDefaultAsync(j => j.Title == "Mobile App for Fitness Tracking");

        if (jobPost1 == null || jobPost2 == null) return;

        // Seed Proposals first — Project.AcceptedProposalId is a required (non-nullable) FK,
        // so the accepted Proposal must exist before the Project row can be inserted.
        var proposal1 = new Proposal
        {
            JobId = jobPost1.Id,
            ExpertId = users.FirstOrDefault(u => u.Email == "expert1@example.com")!.Id,
            CoverLetter = "I have 5+ years experience building e-commerce sites. I can deliver a high-quality React and Node.js solution within your timeline and budget.",
            ProposedBudget = 7000,
            ProposedTimelineDays = 25,
            Status = ProposalStatus.ACCEPTED,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var proposal2 = new Proposal
        {
            JobId = jobPost2.Id,
            ExpertId = users.FirstOrDefault(u => u.Email == "expert3@example.com")!.Id,
            CoverLetter = "Experienced mobile developer with 4+ years building cross-platform apps. I can create a robust fitness tracking app with all the requested features.",
            ProposedBudget = 12000,
            ProposedTimelineDays = 40,
            Status = ProposalStatus.ACCEPTED,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _context.Proposals.AddRangeAsync(new[] { proposal1, proposal2 });
        await _context.SaveChangesAsync();

        // Project 1 - From Job Post 1
        var project1 = new Project
        {
            JobId = jobPost1.Id,
            ClientId = jobPost1.ClientId,
            ExpertId = users.FirstOrDefault(u => u.Email == "expert1@example.com")!.Id,
            AcceptedProposalId = proposal1.Id,
            Title = "E-commerce Website Development",
            Description = "Building a modern e-commerce platform with React and Node.js",
            TotalBudget = 7000,
            Currency = "AICOIN",
            Status = ProjectStatus.ACTIVE,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Project 2 - From Job Post 2
        var project2 = new Project
        {
            JobId = jobPost2.Id,
            ClientId = jobPost2.ClientId,
            ExpertId = users.FirstOrDefault(u => u.Email == "expert3@example.com")!.Id,
            AcceptedProposalId = proposal2.Id,
            Title = "Fitness Tracking Mobile App",
            Description = "Cross-platform fitness tracking app for iOS and Android",
            TotalBudget = 12000,
            Currency = "AICOIN",
            Status = ProjectStatus.IN_REVIEW,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _context.Projects.AddRangeAsync(new[] { project1, project2 });
        await _context.SaveChangesAsync();

        // Seed Proposal Milestones
        var proposalMilestone1 = new ProposalMilestone
        {
            ProposalId = proposal1.Id,
            Title = "Design & Planning",
            Description = "UI/UX design and project planning phase",
            Amount = 2000,
            OrderIndex = 1
        };

        var proposalMilestone2 = new ProposalMilestone
        {
            ProposalId = proposal1.Id,
            Title = "Frontend Development",
            Description = "React frontend development with responsive design",
            Amount = 2500,
            OrderIndex = 2
        };

        var proposalMilestone3 = new ProposalMilestone
        {
            ProposalId = proposal1.Id,
            Title = "Backend Development & Deployment",
            Description = "Node.js backend, database, and deployment",
            Amount = 2500,
            OrderIndex = 3
        };

        var proposalMilestone4 = new ProposalMilestone
        {
            ProposalId = proposal2.Id,
            Title = "Planning & Design",
            Description = "App planning and UI design",
            Amount = 3000,
            OrderIndex = 1
        };

        var proposalMilestone5 = new ProposalMilestone
        {
            ProposalId = proposal2.Id,
            Title = "Core Development",
            Description = "Main app features and functionality",
            Amount = 5000,
            OrderIndex = 2
        };

        var proposalMilestone6 = new ProposalMilestone
        {
            ProposalId = proposal2.Id,
            Title = "Testing & Deployment",
            Description = "Testing, optimization, and app store deployment",
            Amount = 4000,
            OrderIndex = 3
        };

        await _context.ProposalMilestones.AddRangeAsync(new[]
        {
            proposalMilestone1, proposalMilestone2, proposalMilestone3,
            proposalMilestone4, proposalMilestone5, proposalMilestone6
        });
        await _context.SaveChangesAsync();

        // Seed Milestones
        var milestone1 = new Milestone
        {
            ProjectId = project1.Id,
            Title = "Design & Planning",
            Description = "UI/UX design and project planning",
            AcceptanceCriteria = "Complete wireframes, mockups, and project plan",
            Amount = 2000,
            Currency = "AICOIN",
            OrderIndex = 1,
            Status = MilestoneStatus.APPROVED,
            FundedAt = DateTimeOffset.UtcNow.AddDays(-8),
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-7),
            ApprovedAt = DateTimeOffset.UtcNow.AddDays(-6),
            PaidAt = DateTimeOffset.UtcNow.AddDays(-6),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var milestone2 = new Milestone
        {
            ProjectId = project1.Id,
            Title = "Frontend Development",
            Description = "React frontend development with responsive design",
            AcceptanceCriteria = "Complete React app with all features implemented",
            Amount = 2500,
            Currency = "AICOIN",
            OrderIndex = 2,
            Status = MilestoneStatus.IN_PROGRESS,
            FundedAt = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var milestone3 = new Milestone
        {
            ProjectId = project1.Id,
            Title = "Backend Development & Deployment",
            Description = "Node.js backend, database, and deployment",
            AcceptanceCriteria = "Complete API, database setup, and deployment",
            Amount = 2500,
            Currency = "AICOIN",
            OrderIndex = 3,
            Status = MilestoneStatus.CREATED,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var milestone4 = new Milestone
        {
            ProjectId = project2.Id,
            Title = "Planning & Design",
            Description = "App planning and UI design",
            AcceptanceCriteria = "Complete app planning and UI design mockups",
            Amount = 3000,
            Currency = "AICOIN",
            OrderIndex = 1,
            Status = MilestoneStatus.APPROVED,
            FundedAt = DateTimeOffset.UtcNow.AddDays(-4),
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-3),
            ApprovedAt = DateTimeOffset.UtcNow.AddDays(-2),
            PaidAt = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var milestone5 = new Milestone
        {
            ProjectId = project2.Id,
            Title = "Core Development",
            Description = "Main app features and functionality",
            AcceptanceCriteria = "Complete core features integration",
            Amount = 5000,
            Currency = "AICOIN",
            OrderIndex = 2,
            Status = MilestoneStatus.SUBMITTED,
            FundedAt = DateTimeOffset.UtcNow.AddDays(-1),
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _context.Milestones.AddRangeAsync(new[] { milestone1, milestone2, milestone3, milestone4, milestone5 });
        await _context.SaveChangesAsync();

        // Seed Deliverables
        var deliverable1 = new Deliverable
        {
            MilestoneId = milestone1.Id,
            ExpertId = project1.ExpertId,
            Description = "Complete UI/UX design mockups",
            FileUrl = "/files/design-mockups.zip",
            Status = DeliverableStatus.APPROVED,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var deliverable2 = new Deliverable
        {
            MilestoneId = milestone2.Id,
            ExpertId = project1.ExpertId,
            Description = "Frontend development progress",
            FileUrl = "/files/react-app-progress.zip",
            Status = DeliverableStatus.SUBMITTED,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var deliverable3 = new Deliverable
        {
            MilestoneId = milestone4.Id,
            ExpertId = project2.ExpertId,
            Description = "Complete app design mockups",
            FileUrl = "/files/app-design-mockups.zip",
            Status = DeliverableStatus.APPROVED,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var deliverable4 = new Deliverable
        {
            MilestoneId = milestone5.Id,
            ExpertId = project2.ExpertId,
            Description = "Main app features implementation",
            FileUrl = "/files/core-features.zip",
            Status = DeliverableStatus.SUBMITTED,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _context.Deliverables.AddRangeAsync(new[] { deliverable1, deliverable2, deliverable3, deliverable4 });
        await _context.SaveChangesAsync();

        // Seed Reviews for completed project
        var review1 = new Review
        {
            ProjectId = project2.Id,
            ReviewerId = project2.ClientId,
            RevieweeId = project2.ExpertId,
            Rating = 5,
            Comment = "Excellent work! Delivered on time and quality exceeded expectations. The app is performing well and users love the interface.",
            CommunicationRating = 5,
            QualityRating = 5,
            DeadlineRating = 5,
            RequirementClarityRating = 4,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        await _context.Reviews.AddAsync(review1);
        await _context.SaveChangesAsync();

        // Seed Wallets for users
        var wallets = new List<Wallet>
        {
            // Admin wallets
            new Wallet
            {
                UserId = users.FirstOrDefault(u => u.Email == "admin@aivora.com")!.Id,
                AvailableBalance = 100000,
                Currency = "AICOIN",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Wallet
            {
                UserId = users.FirstOrDefault(u => u.Email == "ahihi@aivora.com")!.Id,
                AvailableBalance = 50000,
                Currency = "AICOIN",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            // Client wallets
            new Wallet
            {
                UserId = users.FirstOrDefault(u => u.Email == "client1@example.com")!.Id,
                AvailableBalance = 10000,
                Currency = "AICOIN",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Wallet
            {
                UserId = users.FirstOrDefault(u => u.Email == "client2@example.com")!.Id,
                AvailableBalance = 8000,
                Currency = "AICOIN",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            // Expert wallets
            new Wallet
            {
                UserId = users.FirstOrDefault(u => u.Email == "expert1@example.com")!.Id,
                AvailableBalance = 5000,
                Currency = "AICOIN",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Wallet
            {
                UserId = users.FirstOrDefault(u => u.Email == "expert2@example.com")!.Id,
                AvailableBalance = 3000,
                Currency = "AICOIN",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Wallet
            {
                UserId = users.FirstOrDefault(u => u.Email == "expert3@example.com")!.Id,
                AvailableBalance = 7000,
                Currency = "AICOIN",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Wallet
            {
                UserId = users.FirstOrDefault(u => u.Email == "expert4@example.com")!.Id,
                AvailableBalance = 10000,
                Currency = "AICOIN",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        await _context.Wallets.AddRangeAsync(wallets);
        await _context.SaveChangesAsync();

        // Seed Wallet Transactions - Temporarily commented out due to dependency issues
        // var walletTransactions = new List<WalletTransaction>
        // {
        //     // Will be added back when payment dependency is resolved
        // };
        // await _context.WalletTransactions.AddRangeAsync(walletTransactions);
        // await _context.SaveChangesAsync();

        // Seed Payments
        var payments = new List<Payment>
        {
            new Payment
            {
                ProjectId = project1.Id,
                MilestoneId = milestone1.Id,
                PayerId = project1.ClientId,
                PayeeId = project1.ExpertId,
                Amount = 2000,
                Currency = "AICOIN",
                Status = PaymentStatus.RELEASED,
                ReleasedAt = DateTimeOffset.UtcNow.AddDays(-6),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-6),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-6)
            },
            new Payment
            {
                ProjectId = project2.Id,
                MilestoneId = milestone4.Id,
                PayerId = project2.ClientId,
                PayeeId = project2.ExpertId,
                Amount = 3000,
                Currency = "AICOIN",
                Status = PaymentStatus.RELEASED,
                ReleasedAt = DateTimeOffset.UtcNow.AddDays(-2),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2)
            }
        };

        await _context.Payments.AddRangeAsync(payments);
        await _context.SaveChangesAsync();
    }

    private string HashPassword(string password)
    {
        // Use BCrypt for consistent password hashing with IdentityService
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}