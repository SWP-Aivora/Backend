/* =========================================================
   AITasker MVP Compatibility Schema - Simulated Direct Transfer
   Source of truth: docs/flows/MainFlows-new.md

   SQL Server safe draft.

   Compatibility semantics:
   - Wallets = simulated demo balances only; not legal wallet/custody.
   - Payments = simulated direct-transfer records for milestones.
   - WalletTransactions = simulated transfer/event ledger.
   - Keep legacy technical names to minimize current code changes.

   PaymentStatus mapping:
   - PENDING = record created
   - HELD = transfer initiated / waiting receipt confirmation
   - RELEASED = receipt/payment completed as a legacy technical value
   - FAILED = simulated transfer failed
   - FROZEN / REFUNDED / PARTIALLY_RELEASED = legacy escrow states, not used in new main flow

   MilestoneStatus mapping:
   - FUNDED = simulated direct transfer recorded / receipt ready, not escrow funded
   - PAID = deliverable approved and milestone completed, not platform payout
   ========================================================= */

-- USE AITaskerDb;
-- GO

/* =========================================================
   DROP TABLES IF EXIST
   ========================================================= */

DROP TABLE IF EXISTS DisputeEvidence;
DROP TABLE IF EXISTS Disputes;
DROP TABLE IF EXISTS Reviews;
DROP TABLE IF EXISTS Messages;
DROP TABLE IF EXISTS Conversations;
DROP TABLE IF EXISTS WalletTransactions;
DROP TABLE IF EXISTS Payments;
DROP TABLE IF EXISTS Wallets;
DROP TABLE IF EXISTS Deliverables;
DROP TABLE IF EXISTS Milestones;
DROP TABLE IF EXISTS Projects;
DROP TABLE IF EXISTS ProposalMilestones;
DROP TABLE IF EXISTS Proposals;
DROP TABLE IF EXISTS RecommendationResults;
DROP TABLE IF EXISTS AIJobSuggestions;
DROP TABLE IF EXISTS JobSkills;
DROP TABLE IF EXISTS JobPosts;
DROP TABLE IF EXISTS ExpertSkills;
DROP TABLE IF EXISTS Skills;
DROP TABLE IF EXISTS Categories;
DROP TABLE IF EXISTS ExpertProfiles;
DROP TABLE IF EXISTS ClientProfiles;
DROP TABLE IF EXISTS Users;
GO

/* =========================================================
   USERS & PROFILES
   ========================================================= */

CREATE TABLE Users (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    FullName NVARCHAR(255) NOT NULL,
    AvatarUrl NVARCHAR(MAX) NULL,
    Phone NVARCHAR(30) NULL,
    Role VARCHAR(20) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastLoginAt DATETIME2 NULL,

    CONSTRAINT CK_Users_Role CHECK (Role IN ('CLIENT', 'EXPERT', 'ADMIN')),
    CONSTRAINT CK_Users_Status CHECK (Status IN ('PENDING', 'ACTIVE', 'SUSPENDED', 'DELETED'))
);
GO

CREATE TABLE ClientProfiles (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    CompanyName NVARCHAR(255) NULL,
    Industry NVARCHAR(255) NULL,
    CompanySize NVARCHAR(100) NULL,
    Website NVARCHAR(MAX) NULL,
    Description NVARCHAR(MAX) NULL,
    Rating DECIMAL(3, 2) NOT NULL DEFAULT 0,
    TotalReviews INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_ClientProfiles_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT CK_ClientProfiles_Rating CHECK (Rating >= 0 AND Rating <= 5),
    CONSTRAINT CK_ClientProfiles_TotalReviews CHECK (TotalReviews >= 0)
);
GO

CREATE TABLE ExpertProfiles (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    Title NVARCHAR(255) NULL,
    Bio NVARCHAR(MAX) NULL,
    HourlyRate DECIMAL(12, 2) NULL,
    ExperienceYears INT NOT NULL DEFAULT 0,
    AvailabilityStatus VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE',
    Rating DECIMAL(3, 2) NOT NULL DEFAULT 0,
    TotalReviews INT NOT NULL DEFAULT 0,
    CompletedProjects INT NOT NULL DEFAULT 0,
    SuccessRate DECIMAL(5, 2) NOT NULL DEFAULT 0,
    ResponseTimeMinutes INT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_ExpertProfiles_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT CK_ExpertProfiles_Availability CHECK (AvailabilityStatus IN ('AVAILABLE', 'BUSY', 'UNAVAILABLE')),
    CONSTRAINT CK_ExpertProfiles_HourlyRate CHECK (HourlyRate IS NULL OR HourlyRate >= 0),
    CONSTRAINT CK_ExpertProfiles_Rating CHECK (Rating >= 0 AND Rating <= 5),
    CONSTRAINT CK_ExpertProfiles_TotalReviews CHECK (TotalReviews >= 0),
    CONSTRAINT CK_ExpertProfiles_CompletedProjects CHECK (CompletedProjects >= 0),
    CONSTRAINT CK_ExpertProfiles_SuccessRate CHECK (SuccessRate >= 0 AND SuccessRate <= 100)
);
GO

/* =========================================================
   CATEGORIES, SKILLS & JOB POSTS
   ========================================================= */

CREATE TABLE Categories (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(255) NOT NULL UNIQUE,
    Description NVARCHAR(MAX) NULL,
    ParentId UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentId) REFERENCES Categories(Id)
);
GO

CREATE TABLE Skills (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(255) NOT NULL UNIQUE,
    CategoryId UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Skills_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);
GO

CREATE TABLE ExpertSkills (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ExpertId UNIQUEIDENTIFIER NOT NULL,
    SkillId UNIQUEIDENTIFIER NOT NULL,
    Level VARCHAR(30) NOT NULL DEFAULT 'INTERMEDIATE',
    YearsExperience INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_ExpertSkills_Users FOREIGN KEY (ExpertId) REFERENCES Users(Id),
    CONSTRAINT FK_ExpertSkills_Skills FOREIGN KEY (SkillId) REFERENCES Skills(Id),
    CONSTRAINT UQ_ExpertSkills_Expert_Skill UNIQUE (ExpertId, SkillId),
    CONSTRAINT CK_ExpertSkills_Level CHECK (Level IN ('BEGINNER', 'INTERMEDIATE', 'ADVANCED', 'EXPERT')),
    CONSTRAINT CK_ExpertSkills_Years CHECK (YearsExperience >= 0)
);
GO

CREATE TABLE JobPosts (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ClientId UNIQUEIDENTIFIER NOT NULL,
    CategoryId UNIQUEIDENTIFIER NULL,
    Title NVARCHAR(255) NOT NULL,
    OriginalDescription NVARCHAR(MAX) NOT NULL,
    FinalDescription NVARCHAR(MAX) NULL,
    BusinessDomain NVARCHAR(255) NULL,
    ExpectedOutcome NVARCHAR(MAX) NULL,
    BudgetType VARCHAR(20) NOT NULL DEFAULT 'FIXED',
    BudgetMin DECIMAL(12, 2) NULL,
    BudgetMax DECIMAL(12, 2) NULL,
    Currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    TimelineDays INT NULL,
    Deadline DATE NULL,
    ExperienceLevel VARCHAR(50) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'DRAFT',
    Visibility VARCHAR(50) NOT NULL DEFAULT 'PUBLIC',
    PublishedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_JobPosts_Client FOREIGN KEY (ClientId) REFERENCES Users(Id),
    CONSTRAINT FK_JobPosts_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id),
    CONSTRAINT CK_JobPosts_BudgetType CHECK (BudgetType IN ('FIXED', 'HOURLY')),
    CONSTRAINT CK_JobPosts_ExperienceLevel CHECK (ExperienceLevel IS NULL OR ExperienceLevel IN ('BEGINNER', 'INTERMEDIATE', 'ADVANCED', 'EXPERT')),
    CONSTRAINT CK_JobPosts_Status CHECK (Status IN ('DRAFT', 'OPEN', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED', 'CLOSED')),
    CONSTRAINT CK_JobPosts_Visibility CHECK (Visibility IN ('PUBLIC', 'PRIVATE', 'INVITED_ONLY')),
    CONSTRAINT CK_JobPosts_Budget CHECK (BudgetMin IS NULL OR BudgetMax IS NULL OR BudgetMin <= BudgetMax)
);
GO

CREATE TABLE JobSkills (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    JobId UNIQUEIDENTIFIER NOT NULL,
    SkillId UNIQUEIDENTIFIER NOT NULL,
    IsRequired BIT NOT NULL DEFAULT 1,

    CONSTRAINT FK_JobSkills_JobPosts FOREIGN KEY (JobId) REFERENCES JobPosts(Id),
    CONSTRAINT FK_JobSkills_Skills FOREIGN KEY (SkillId) REFERENCES Skills(Id),
    CONSTRAINT UQ_JobSkills_Job_Skill UNIQUE (JobId, SkillId)
);
GO

CREATE TABLE AIJobSuggestions (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    JobId UNIQUEIDENTIFIER NULL,
    ClientId UNIQUEIDENTIFIER NOT NULL,
    RawInput NVARCHAR(MAX) NOT NULL,
    SuggestedTitle NVARCHAR(255) NULL,
    SuggestedDescription NVARCHAR(MAX) NULL,
    SuggestedBudgetType VARCHAR(20) NOT NULL DEFAULT 'FIXED',
    Currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    SuggestedBudgetMin DECIMAL(12, 2) NULL,
    SuggestedBudgetMax DECIMAL(12, 2) NULL,
    SuggestedTimelineDays INT NULL,
    SuggestedExperienceLevel VARCHAR(50) NULL,
    SuggestedBusinessDomain NVARCHAR(255) NULL,
    SuggestedExpectedOutcome NVARCHAR(MAX) NULL,
    SuggestedSkillsJson NVARCHAR(MAX) NULL,
    SuggestedMilestonesJson NVARCHAR(MAX) NULL,
    ClarifyingQuestionsJson NVARCHAR(MAX) NULL,
    ClarifyingAnswersJson NVARCHAR(MAX) NULL,
    RiskWarningsJson NVARCHAR(MAX) NULL,
    AIModel NVARCHAR(100) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'GENERATED',
    RejectionReason NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_AIJobSuggestions_JobPosts FOREIGN KEY (JobId) REFERENCES JobPosts(Id),
    CONSTRAINT FK_AIJobSuggestions_Client FOREIGN KEY (ClientId) REFERENCES Users(Id),
    CONSTRAINT CK_AIJobSuggestions_BudgetType CHECK (SuggestedBudgetType IN ('FIXED', 'HOURLY')),
    CONSTRAINT CK_AIJobSuggestions_ExperienceLevel CHECK (SuggestedExperienceLevel IS NULL OR SuggestedExperienceLevel IN ('BEGINNER', 'INTERMEDIATE', 'ADVANCED', 'EXPERT')),
    CONSTRAINT CK_AIJobSuggestions_Status CHECK (Status IN ('GENERATED', 'ACCEPTED', 'REJECTED', 'FAILED'))
);
GO

CREATE TABLE RecommendationResults (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    JobId UNIQUEIDENTIFIER NOT NULL,
    ExpertId UNIQUEIDENTIFIER NOT NULL,
    TotalScore DECIMAL(5, 2) NOT NULL DEFAULT 0,
    Explanation NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_RecommendationResults_JobPosts FOREIGN KEY (JobId) REFERENCES JobPosts(Id),
    CONSTRAINT FK_RecommendationResults_Experts FOREIGN KEY (ExpertId) REFERENCES Users(Id),
    CONSTRAINT UQ_RecommendationResults_Job_Expert UNIQUE (JobId, ExpertId),
    CONSTRAINT CK_RecommendationResults_TotalScore CHECK (TotalScore >= 0 AND TotalScore <= 100)
);
GO

/* =========================================================
   PROPOSALS
   ========================================================= */

CREATE TABLE Proposals (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    JobId UNIQUEIDENTIFIER NOT NULL,
    ExpertId UNIQUEIDENTIFIER NOT NULL,
    CoverLetter NVARCHAR(MAX) NOT NULL,
    ProposedBudget DECIMAL(12, 2) NOT NULL,
    ProposedTimelineDays INT NULL,
    Currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    Status VARCHAR(30) NOT NULL DEFAULT 'SUBMITTED',
    SubmittedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    WithdrawnAt DATETIME2 NULL,

    CONSTRAINT FK_Proposals_JobPosts FOREIGN KEY (JobId) REFERENCES JobPosts(Id),
    CONSTRAINT FK_Proposals_Experts FOREIGN KEY (ExpertId) REFERENCES Users(Id),
    CONSTRAINT UQ_Proposals_Job_Expert UNIQUE (JobId, ExpertId),
    CONSTRAINT CK_Proposals_Status CHECK (Status IN ('SUBMITTED', 'SHORTLISTED', 'ACCEPTED', 'REJECTED', 'WITHDRAWN')),
    CONSTRAINT CK_Proposals_Budget CHECK (ProposedBudget >= 0)
);
GO

CREATE TABLE ProposalMilestones (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProposalId UNIQUEIDENTIFIER NOT NULL,
    Title NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Amount DECIMAL(12, 2) NULL,
    DueDays INT NULL,
    AcceptanceCriteria NVARCHAR(MAX) NULL,
    OrderIndex INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_ProposalMilestones_Proposals FOREIGN KEY (ProposalId) REFERENCES Proposals(Id),
    CONSTRAINT CK_ProposalMilestones_Amount CHECK (Amount IS NULL OR Amount >= 0)
);
GO

/* =========================================================
   PROJECTS, MILESTONES & PAYMENT COMPATIBILITY
   ========================================================= */

CREATE TABLE Projects (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    JobId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    AcceptedProposalId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    ClientId UNIQUEIDENTIFIER NOT NULL,
    ExpertId UNIQUEIDENTIFIER NOT NULL,
    Title NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    TotalBudget DECIMAL(12, 2) NULL,
    Currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    Status VARCHAR(30) NOT NULL DEFAULT 'PENDING_PAYMENT',
    StartDate DATE NULL,
    EndDate DATE NULL,
    CompletedAt DATETIME2 NULL,
    CancelledAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Projects_JobPosts FOREIGN KEY (JobId) REFERENCES JobPosts(Id),
    CONSTRAINT FK_Projects_Proposals FOREIGN KEY (AcceptedProposalId) REFERENCES Proposals(Id),
    CONSTRAINT FK_Projects_Client FOREIGN KEY (ClientId) REFERENCES Users(Id),
    CONSTRAINT FK_Projects_Expert FOREIGN KEY (ExpertId) REFERENCES Users(Id),
    CONSTRAINT CK_Projects_Status CHECK (Status IN ('PENDING_PAYMENT', 'ACTIVE', 'IN_REVIEW', 'DISPUTED', 'COMPLETED', 'CANCELLED')),
    CONSTRAINT CK_Projects_Budget CHECK (TotalBudget IS NULL OR TotalBudget >= 0)
);
GO

CREATE TABLE Milestones (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NOT NULL,
    Title NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    AcceptanceCriteria NVARCHAR(MAX) NULL,
    Amount DECIMAL(12, 2) NOT NULL,
    Currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    DueDate DATE NULL,
    OrderIndex INT NOT NULL DEFAULT 0,
    Status VARCHAR(30) NOT NULL DEFAULT 'CREATED',
    FundedAt DATETIME2 NULL,
    SubmittedAt DATETIME2 NULL,
    ApprovedAt DATETIME2 NULL,
    PaidAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Milestones_Projects FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
    CONSTRAINT CK_Milestones_Status CHECK (
        Status IN (
            'CREATED',
            'FUNDED',
            'IN_PROGRESS',
            'SUBMITTED',
            'REVISION_REQUESTED',
            'APPROVED',
            'DISPUTED',
            'PAID',
            'REFUNDED'
        )
    ),
    CONSTRAINT CK_Milestones_Amount CHECK (Amount >= 0)
);
GO

-- Simulated demo balance only. This table is not a legal wallet or custody ledger.
CREATE TABLE Wallets (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    AvailableBalance DECIMAL(12, 2) NOT NULL DEFAULT 0,
    HeldBalance DECIMAL(12, 2) NOT NULL DEFAULT 0,
    TotalEarned DECIMAL(12, 2) NOT NULL DEFAULT 0,
    Currency VARCHAR(10) NOT NULL DEFAULT 'AICOIN',
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Wallets_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT CK_Wallets_AvailableBalance CHECK (AvailableBalance >= 0),
    CONSTRAINT CK_Wallets_HeldBalance CHECK (HeldBalance >= 0),
    CONSTRAINT CK_Wallets_TotalEarned CHECK (TotalEarned >= 0)
);
GO

-- Direct-transfer tracking record. Legacy status names are kept for code compatibility.
CREATE TABLE Payments (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NOT NULL,
    MilestoneId UNIQUEIDENTIFIER NOT NULL,
    PayerId UNIQUEIDENTIFIER NOT NULL,
    PayeeId UNIQUEIDENTIFIER NOT NULL,
    Amount DECIMAL(12, 2) NOT NULL,
    Currency VARCHAR(10) NOT NULL DEFAULT 'AICOIN',
    Status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    HeldAt DATETIME2 NULL,
    ReleasedAt DATETIME2 NULL,
    RefundedAt DATETIME2 NULL,
    FrozenAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Payments_Projects FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
    CONSTRAINT FK_Payments_Milestones FOREIGN KEY (MilestoneId) REFERENCES Milestones(Id),
    CONSTRAINT FK_Payments_Payer FOREIGN KEY (PayerId) REFERENCES Users(Id),
    CONSTRAINT FK_Payments_Payee FOREIGN KEY (PayeeId) REFERENCES Users(Id),
    CONSTRAINT UQ_Payments_Milestone UNIQUE (MilestoneId),
    CONSTRAINT CK_Payments_Status CHECK (Status IN ('PENDING', 'HELD', 'RELEASED', 'REFUNDED', 'FROZEN', 'FAILED', 'PARTIALLY_RELEASED')),
    CONSTRAINT CK_Payments_Amount CHECK (Amount >= 0)
);
GO

-- Simulated transfer/event ledger. Legacy type names may remain for compatibility.
CREATE TABLE WalletTransactions (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    WalletId UNIQUEIDENTIFIER NOT NULL,
    PaymentId UNIQUEIDENTIFIER NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Type VARCHAR(50) NOT NULL,
    Direction VARCHAR(10) NOT NULL,
    Amount DECIMAL(12, 2) NOT NULL,
    BalanceBefore DECIMAL(12, 2) NOT NULL DEFAULT 0,
    BalanceAfter DECIMAL(12, 2) NOT NULL DEFAULT 0,
    Description NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_WalletTransactions_Wallets FOREIGN KEY (WalletId) REFERENCES Wallets(Id),
    CONSTRAINT FK_WalletTransactions_Payments FOREIGN KEY (PaymentId) REFERENCES Payments(Id),
    CONSTRAINT FK_WalletTransactions_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT CK_WalletTransactions_Type CHECK (
        Type IN (
            'DEMO_DEPOSIT',
            'ESCROW_HOLD',
            'PAYMENT_RELEASE',
            'REFUND',
            'WITHDRAWAL_REQUEST',
            'WITHDRAWAL_COMPLETED'
        )
    ),
    CONSTRAINT CK_WalletTransactions_Direction CHECK (Direction IN ('CREDIT', 'DEBIT')),
    CONSTRAINT CK_WalletTransactions_Amount CHECK (Amount >= 0)
);
GO

CREATE TABLE Deliverables (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    MilestoneId UNIQUEIDENTIFIER NOT NULL,
    ExpertId UNIQUEIDENTIFIER NOT NULL,
    Description NVARCHAR(MAX) NULL,
    FileUrl NVARCHAR(MAX) NULL,
    DemoUrl NVARCHAR(MAX) NULL,
    SourceCodeUrl NVARCHAR(MAX) NULL,
    Note NVARCHAR(MAX) NULL,
    RevisionNumber INT NOT NULL DEFAULT 1,
    Status VARCHAR(30) NOT NULL DEFAULT 'SUBMITTED',
    SubmittedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ReviewedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Deliverables_Milestones FOREIGN KEY (MilestoneId) REFERENCES Milestones(Id),
    CONSTRAINT FK_Deliverables_Experts FOREIGN KEY (ExpertId) REFERENCES Users(Id),
    CONSTRAINT CK_Deliverables_Status CHECK (Status IN ('SUBMITTED', 'APPROVED', 'REVISION_REQUESTED', 'REJECTED')),
    CONSTRAINT CK_Deliverables_Revision CHECK (RevisionNumber >= 1)
);
GO

/* =========================================================
   COMMUNICATION, REVIEWS & DISPUTES
   ========================================================= */

CREATE TABLE Conversations (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NULL,
    JobId UNIQUEIDENTIFIER NULL,
    ClientId UNIQUEIDENTIFIER NOT NULL,
    ExpertId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Conversations_Projects FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
    CONSTRAINT FK_Conversations_JobPosts FOREIGN KEY (JobId) REFERENCES JobPosts(Id),
    CONSTRAINT FK_Conversations_Client FOREIGN KEY (ClientId) REFERENCES Users(Id),
    CONSTRAINT FK_Conversations_Expert FOREIGN KEY (ExpertId) REFERENCES Users(Id)
);
GO

CREATE TABLE Messages (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ConversationId UNIQUEIDENTIFIER NOT NULL,
    SenderId UNIQUEIDENTIFIER NOT NULL,
    Content NVARCHAR(MAX) NULL,
    AttachmentUrl NVARCHAR(MAX) NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ReadAt DATETIME2 NULL,

    CONSTRAINT FK_Messages_Conversations FOREIGN KEY (ConversationId) REFERENCES Conversations(Id),
    CONSTRAINT FK_Messages_Sender FOREIGN KEY (SenderId) REFERENCES Users(Id)
);
GO

CREATE TABLE Reviews (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NOT NULL,
    ReviewerId UNIQUEIDENTIFIER NOT NULL,
    RevieweeId UNIQUEIDENTIFIER NOT NULL,
    Rating INT NOT NULL,
    Comment NVARCHAR(MAX) NULL,
    CommunicationRating INT NULL,
    QualityRating INT NULL,
    DeadlineRating INT NULL,
    RequirementClarityRating INT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Reviews_Projects FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
    CONSTRAINT FK_Reviews_Reviewer FOREIGN KEY (ReviewerId) REFERENCES Users(Id),
    CONSTRAINT FK_Reviews_Reviewee FOREIGN KEY (RevieweeId) REFERENCES Users(Id),
    CONSTRAINT UQ_Reviews_Project_Reviewer_Reviewee UNIQUE (ProjectId, ReviewerId, RevieweeId),
    CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5),
    CONSTRAINT CK_Reviews_CommunicationRating CHECK (CommunicationRating IS NULL OR CommunicationRating BETWEEN 1 AND 5),
    CONSTRAINT CK_Reviews_QualityRating CHECK (QualityRating IS NULL OR QualityRating BETWEEN 1 AND 5),
    CONSTRAINT CK_Reviews_DeadlineRating CHECK (DeadlineRating IS NULL OR DeadlineRating BETWEEN 1 AND 5),
    CONSTRAINT CK_Reviews_RequirementClarityRating CHECK (RequirementClarityRating IS NULL OR RequirementClarityRating BETWEEN 1 AND 5),
    CONSTRAINT CK_Reviews_NotSelf CHECK (ReviewerId <> RevieweeId)
);
GO

CREATE TABLE Disputes (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NOT NULL,
    MilestoneId UNIQUEIDENTIFIER NOT NULL,
    PaymentId UNIQUEIDENTIFIER NOT NULL,
    OpenedBy UNIQUEIDENTIFIER NOT NULL,
    AgainstUserId UNIQUEIDENTIFIER NULL,
    Reason NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'OPEN',
    AdminId UNIQUEIDENTIFIER NULL,
    ResolutionNote NVARCHAR(MAX) NULL,
    ResolvedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Disputes_Projects FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
    CONSTRAINT FK_Disputes_Milestones FOREIGN KEY (MilestoneId) REFERENCES Milestones(Id),
    CONSTRAINT FK_Disputes_Payments FOREIGN KEY (PaymentId) REFERENCES Payments(Id),
    CONSTRAINT FK_Disputes_OpenedBy FOREIGN KEY (OpenedBy) REFERENCES Users(Id),
    CONSTRAINT FK_Disputes_AgainstUser FOREIGN KEY (AgainstUserId) REFERENCES Users(Id),
    CONSTRAINT FK_Disputes_Admin FOREIGN KEY (AdminId) REFERENCES Users(Id),
    CONSTRAINT CK_Disputes_Status CHECK (Status IN ('OPEN', 'UNDER_REVIEW', 'RESOLVED', 'CLOSED'))
);
GO

CREATE TABLE DisputeEvidence (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    DisputeId UNIQUEIDENTIFIER NOT NULL,
    SubmittedBy UNIQUEIDENTIFIER NOT NULL,
    Content NVARCHAR(MAX) NULL,
    FileUrl NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_DisputeEvidence_Disputes FOREIGN KEY (DisputeId) REFERENCES Disputes(Id),
    CONSTRAINT FK_DisputeEvidence_Users FOREIGN KEY (SubmittedBy) REFERENCES Users(Id)
);
GO

/* =========================================================
   INDEXES
   ========================================================= */

CREATE INDEX IX_Users_Role ON Users(Role);
CREATE INDEX IX_Users_Status ON Users(Status);
CREATE INDEX IX_JobPosts_ClientId ON JobPosts(ClientId);
CREATE INDEX IX_JobPosts_Status ON JobPosts(Status);
CREATE INDEX IX_JobPosts_CategoryId ON JobPosts(CategoryId);
CREATE INDEX IX_Proposals_JobId ON Proposals(JobId);
CREATE INDEX IX_Proposals_ExpertId ON Proposals(ExpertId);
CREATE INDEX IX_Proposals_Status ON Proposals(Status);
CREATE INDEX IX_Projects_ClientId ON Projects(ClientId);
CREATE INDEX IX_Projects_ExpertId ON Projects(ExpertId);
CREATE INDEX IX_Projects_Status ON Projects(Status);
CREATE INDEX IX_Milestones_ProjectId ON Milestones(ProjectId);
CREATE INDEX IX_Milestones_Status ON Milestones(Status);
CREATE INDEX IX_Payments_ProjectId ON Payments(ProjectId);
CREATE INDEX IX_Payments_Status ON Payments(Status);
CREATE INDEX IX_WalletTransactions_UserId ON WalletTransactions(UserId);
CREATE INDEX IX_WalletTransactions_PaymentId ON WalletTransactions(PaymentId);
CREATE INDEX IX_Deliverables_MilestoneId ON Deliverables(MilestoneId);
CREATE INDEX IX_Reviews_RevieweeId ON Reviews(RevieweeId);
CREATE INDEX IX_Disputes_Status ON Disputes(Status);
CREATE INDEX IX_Disputes_ProjectId ON Disputes(ProjectId);
CREATE INDEX IX_Messages_ConversationId ON Messages(ConversationId);
CREATE INDEX IX_RecommendationResults_JobId ON RecommendationResults(JobId);
CREATE INDEX IX_RecommendationResults_ExpertId ON RecommendationResults(ExpertId);
CREATE INDEX IX_RecommendationResults_TotalScore ON RecommendationResults(TotalScore);
GO

/* =========================================================
   SEED DATA
   ========================================================= */

INSERT INTO Categories (Name, Description)
VALUES
('Chatbot', 'AI chatbot development and integration'),
('RAG', 'Retrieval-Augmented Generation applications'),
('OCR', 'Document extraction and recognition'),
('AI Automation', 'AI workflow automation'),
('Data Analysis', 'AI-powered data analysis'),
('AI Agent', 'Autonomous AI agent systems');
GO

INSERT INTO Skills (Name)
VALUES
('Python'),
('OpenAI API'),
('Gemini API'),
('RAG'),
('Vector Database'),
('LangChain'),
('Chatbot'),
('OCR'),
('FastAPI'),
('React'),
('Node.js'),
('SQL Server'),
('Prompt Engineering'),
('Automation Workflow');
GO

INSERT INTO Users (Email, PasswordHash, FullName, Role)
VALUES
('client@test.com', 'demo_hash', 'Demo Client', 'CLIENT'),
('expert@test.com', 'demo_hash', 'Demo Expert', 'EXPERT'),
('admin@test.com', 'demo_hash', 'Demo Admin', 'ADMIN');
GO

INSERT INTO ClientProfiles (UserId, CompanyName, Industry)
SELECT Id, 'Beauty Shop Demo', 'E-commerce'
FROM Users
WHERE Email = 'client@test.com';
GO

INSERT INTO ExpertProfiles (
    UserId,
    Title,
    Bio,
    HourlyRate,
    ExperienceYears,
    AvailabilityStatus,
    Rating,
    CompletedProjects,
    SuccessRate
)
SELECT
    Id,
    'AI Chatbot & RAG Developer',
    'I build AI chatbots, RAG systems, and automation workflows.',
    25,
    3,
    'AVAILABLE',
    4.8,
    12,
    95
FROM Users
WHERE Email = 'expert@test.com';
GO

INSERT INTO Wallets (UserId, AvailableBalance, HeldBalance, TotalEarned, Currency)
SELECT Id, 1000, 0, 0, 'AICOIN'
FROM Users
WHERE Email = 'client@test.com';
GO

INSERT INTO Wallets (UserId, AvailableBalance, HeldBalance, TotalEarned, Currency)
SELECT Id, 0, 0, 0, 'AICOIN'
FROM Users
WHERE Email = 'expert@test.com';
GO

/* =========================================================
   QUICK TEST
   ========================================================= */

SELECT * FROM Users;
SELECT * FROM Categories;
SELECT * FROM Skills;
SELECT * FROM Wallets;
GO
