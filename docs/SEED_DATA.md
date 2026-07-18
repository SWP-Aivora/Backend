# Aivora Database Seed Data Documentation

> See also: [`ARCHITECTURE.md`](ARCHITECTURE.md) for the entity model, [`flows/MAINFLOW_v2.md`](flows/MAINFLOW_v2.md) for the business flows this data exercises.

## Overview

This document describes the seed data for the Aivora platform. The seed data is designed to provide a comprehensive dataset that demonstrates all main business flows and features of the platform.

## Seed Data Structure

### 1. Users

#### Admin Users (2)
- **admin@aivora.com** - Main admin account (password: `admin123`)
- **ahihi@aivora.com** - Second admin account (password: `ahihi123`) ⭐ *New*

#### System Account (not for login)
- **system@aivora.com** - `UserRole.SYSTEM`, seeded with an invalid password hash so it can never be logged into. Used internally as the actor for system-generated records (e.g. `SystemConstants.SystemUserId`).

#### Client Users (2)
- **client1@example.com** - Tech Corp (password: `client123`)
- **client2@example.com** - StartupXYZ (password: `client123`)

#### Expert Users (4)
- **expert1@example.com** - Senior Full-stack Developer (password: `expert123`)
- **expert2@example.com** - UI/UX Designer (password: `expert123`)
- **expert3@example.com** - Mobile App Developer (password: `expert123`)
- **expert4@example.com** - ML Engineer (password: `expert123`)

### 2. Categories & Skills

#### Web Development
- Frontend Development
- Backend Development
- Database Design
- API Development
- DevOps
- Testing
- Performance Optimization

#### Mobile Development
- iOS Development
- Android Development
- React Native
- Flutter
- Mobile UI Design
- Mobile API Integration
- App Store Publishing

#### AI/ML
- Machine Learning
- Deep Learning
- NLP
- Computer Vision
- Data Science
- MLOps
- AI Model Deployment

#### Design
- UI Design
- UX Design
- Graphic Design
- Logo Design
- Prototyping
- User Research
- Design Systems

#### Marketing
- SEO
- Content Marketing
- Social Media Marketing
- PPC Advertising
- Email Marketing
- Analytics
- Brand Strategy

### 3. Job Posts (3)

#### Job 1: E-commerce Website
- **Title:** Build E-commerce Website
- **Client:** Tech Corp (client1@example.com)
- **Category:** Web Development
- **Budget:** $5,000 - $8,000 (Fixed)
- **Timeline:** 30 days
- **Status:** OPEN
- **Skills:** Frontend Development, Backend Development, Database Design

#### Job 2: Fitness Tracking Mobile App
- **Title:** Mobile App for Fitness Tracking
- **Client:** StartupXYZ (client2@example.com)
- **Category:** Mobile Development
- **Budget:** $10,000 - $15,000 (Fixed)
- **Timeline:** 45 days
- **Status:** OPEN
- **Skills:** React Native, Mobile UI Design, Mobile API Integration

#### Job 3: Website Redesign
- **Title:** Redesign Company Website UI
- **Client:** Tech Corp (client1@example.com)
- **Category:** Design
- **Budget:** $3,000 - $5,000 (Fixed)
- **Timeline:** 20 days
- **Status:** OPEN
- **Skills:** UI Design, UX Design, Prototyping

### 4. Projects (2 Active)

#### Project 1: E-commerce Website Development
- **From Job:** Job 1
- **Client:** Tech Corp
- **Expert:** Expert One (expert1@example.com)
- **Status:** ACTIVE
- **Budget:** 7,000 AICOIN
- **Started:** 10 days ago

**Milestones:**
1. **Design & Planning** ($2,000) - APPROVED
2. **Frontend Development** ($2,500) - IN PROGRESS
3. **Backend Development & Deployment** ($2,500) - CREATED

#### Project 2: Fitness Tracking Mobile App
- **From Job:** Job 2
- **Client:** StartupXYZ
- **Expert:** Expert Three (expert3@example.com)
- **Status:** IN REVIEW (Completed)
- **Budget:** 12,000 AICOIN
- **Started:** 5 days ago, Completed: 1 day ago

**Milestones:**
1. **Planning & Design** ($3,000) - APPROVED
2. **Core Development** ($5,000) - SUBMITTED
3. **Testing & Deployment** ($4,000) - CREATED

### 5. Reviews (1)

#### Client Review for Expert
- **Project:** Fitness Tracking Mobile App
- **Reviewer:** Client Two (client2@example.com)
- **Reviewee:** Expert Three (expert3@example.com)
- **Rating:** 5/5
- **Comment:** "Excellent work! Delivered on time and quality exceeded expectations."

### 6. Wallets & Payments

#### Expert Wallets
- **Expert One:** 5,000 AICOIN balance
- **Expert Two:** 3,000 AICOIN balance
- **Expert Three:** 7,000 AICOIN balance
- **Expert Four:** 10,000 AICOIN balance

#### Completed Payments
- **Milestone 1 (Project 1):** 2,000 AICOIN - Released
- **Milestone 1 (Project 2):** 3,000 AICOIN - Released

### 7. Business Flow Coverage

#### Flow 1: Create Job & Match Expert
✅ **Covered** - 3 open job posts with different categories and skills
✅ **Expert Profiles** - 4 experts with different specializations
✅ **AI Suggestions** - Framework ready for AI job suggestions

#### Flow 2: Proposal, Agreement & Project Creation
✅ **Proposals** - 2 accepted proposals
✅ **Projects** - 2 active projects
✅ **Milestones** - Multiple milestones with different statuses

#### Flow 3: Project Management & Payment
✅ **Active Projects** - Projects in different stages
✅ **Deliverables** - Submitted deliverables with different statuses
✅ **Payments** - Payment tracking with direct transfer simulation
✅ **Reviews** - Completed project review

## How to Run the Seed Data

1. The seed data runs automatically when the application starts
2. Only runs if the database is empty (no existing users)
3. Seed data is idempotent - can be safely run multiple times

## Seeder Location

The seeder is located at `Aivora.Repositories/Data/Seeders/AivoraDataSeeder.cs`:
- **Interface:** `IAivoraDataSeeder`
- **Implementation:** `AivoraDataSeeder`
- **Registration:** Via `SeedingServiceExtensions.cs` in the API layer

## Customization

### Add New Users
Update the `AivoraDataSeeder.cs` file in the `Aivora.Repositories/Data/Seeders/` directory to add new users with appropriate roles and profiles.

### Add New Categories & Skills
Update the `SeedCategoriesAndSkills()` method to add new categories and associated skills.

### Add New Job Posts
Update the `SeedJobPosts()` method to add new job posts with different requirements.

### Add New Projects
Update the `SeedProjectsAndRelated()` method to create new projects with milestones and deliverables.

## Security Notes

- All passwords are hashed using ASP.NET Identity's PasswordHasher
- Admin accounts have elevated privileges
- User data includes proper validation and constraints
- Financial data uses appropriate decimal precision

## Testing Scenarios

The seed data supports the following testing scenarios:

1. **User Authentication** - Login with different user roles
2. **Job Browsing** - View open jobs with filters
3. **Proposal Submission** - Submit proposals to open jobs
4. **Project Management** - Manage active projects and milestones
5. **Payment Processing** - Track payments and wallet transactions
6. **Reviews & Ratings** - Submit and view reviews
7. **Dispute Resolution** - Ready for dispute scenarios
8. **Admin Dashboard** - Admin functions for managing the platform