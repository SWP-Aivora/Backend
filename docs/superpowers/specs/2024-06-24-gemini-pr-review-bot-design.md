# Gemini PR Review Bot Design - Aivora Backend

> **Date:** 2024-06-24  
> **Version:** 1.0  
> **Status:** Approved

---

## 📋 Overview

Design for automated PR review bot using Google Gemini AI to review code changes in Aivora Backend .NET project. Bot provides security scanning, code quality analysis, and automatic approval/rejection based on confidence scoring.

## 🎯 Architecture

### Workflow Overview
```
GitHub PR Event → Size Check → Diff Processing → Gemini Review → Auto-Review Action
     ↓             ↓              ↓               ↓              ↓
  Skip if >1k   Filter files   Security Scan   Code Quality   Approve/
 lines         Skip noise     (fail-fast)     Review         Request Changes
```

### Components

#### 1. GitHub Action Trigger (`.github/workflows/pr-review.yml`)
```yaml
name: Gemini PR Review
on:
  pull_request:
    types: [opened, synchronize]
    branches: [main]

concurrency:
  group: gemini-review-${{ github.event.pull_request.number }}
  cancel-in-progress: true

permissions:
  contents: read
  pull-requests: write
  statuses: write

jobs:
  review:
    runs-on: ubuntu-latest
    if: github.event.pull_request.draft == false
```

#### 2. Review Bot Console App (.NET 10 Console)
- Multi-stage processing
- Security-first approach with fail-fast
- Confidence-based decision making
- Context-aware prompt engineering

## 🔧 Core Features

### 1. Smart PR Processing

#### Size Gate
- Skip PRs > 1000 lines diff
- Avoid excessive API usage
- User-friendly message for large PRs

```csharp
if (totalDiffLines > 1000)
{
    await PostComment("⚠️ PR quá lớn để review tự động. Vui lòng chia nhỏ PR (< 1000 lines).");
    return;
}
```

#### File Filtering
Skip patterns for non-code files:
```csharp
var skipPatterns = new[]
{
    @"\.env\.example$", @"\.gitignore$", @"\.gitattributes$",
    @"\.png$", @"\.jpg$", @"\.woff2?$",
    @"^bin/", @"^obj/", @"^node_modules/",
    @"\.Designer\.cs$",   // Generated files
    @"\.g\.cs$",          // Source generators  
    @"Migrations/",       // EF migrations (too much noise)
    @"test$", @"spec$",   // Test files
    @"Tests/", @"Specs/"  // Test folders
};
```

### 2. Multi-Stage Review Process

#### Stage 1: Security Scan (Fail-Fast)
```csharp
// Critical security issues (confidence >= 80)
- SQL injection vulnerabilities
- XSS risks
- Hardcoded secrets/API keys
- Authentication bypass
- Authorization flaws

if (criticalSecurityIssues.Any())
{
    await SubmitReview(ReviewAction.RequestChanges, 
        "🚨 Critical security issues found - PR blocked");
    return; // Fail fast - skip stage 2
}
```

#### Stage 2: Code Quality Review
```csharp
// Review areas
- Code architecture & patterns
- Performance issues
- Best practices violations
- Error handling
- Naming conventions
- Maintainability
```

### 3. Confidence-Based Decision Making

#### Confidence Scoring
- **0-25**: False positive (ignore)
- **26-49**: Minor nitpick (info only)
- **50-79**: Real issue but not blocking
- **80-99**: Important issue (block if critical)
- **100**: Critical issue (must fix)

#### Review Actions
```csharp
if (criticalIssues.Any(i => i.Category == "security" || i.Category == "bug"))
{
    await SubmitReview(ReviewAction.RequestChanges, 
        "🚨 Critical issues found - PR blocked");
}
else if (mediumIssues.Any())
{
    await SubmitReview(ReviewAction.Approve, 
        "✅ Approved with minor notes");
}
else
{
    await SubmitReview(ReviewAction.Approve, 
        "✅ No issues found - PR approved");
}
```

### 4. Smart Review Management

#### Dismiss Stale Reviews
```csharp
// Dismiss previous bot reviews when new commits arrive
var oldReviews = await github.PullRequest.Reviews.GetAll(prNumber)
    .Where(r => r.User.Login == botName && r.State == "CHANGES_REQUESTED");

foreach (var review in oldReviews)
{
    await github.PullRequest.Reviews.Dismiss(prNumber, review.Id);
}
```

#### Fallback Strategy
```csharp
try
{
    await SubmitReview(ReviewAction.Approve, ...);
}
catch (GitHubException ex) when (ex.StatusCode == 422)
{
    // Self-approval or permission issues
    await PostComment("⚠️ Bot cố g approve nhưng bị giới hạn. PR cần manual review.");
}
```

## 💡 Prompt Engineering

### Context Loading
- Load `CLAUDE.md` for Aivora-specific patterns
- Load project conventions and guidelines
- Security requirements for marketplace platform

### Aivora-Specific Awareness
```markdown
## Aivora Guidelines
- JWT authentication patterns
- PostgreSQL EF Core conventions
- SignalR chat implementation
- Cloudinary file storage integration
- Escrow payment flows
- API response wrapping: { success, message, data, errors }
```

### Stage-Specific Prompts

#### Security Prompt
Focus on:
- SQL injection prevention
- XSS vulnerabilities
- Authentication/authorization
- Payment security
- Data validation

#### Code Quality Prompt
Focus on:
- SOLID principles
- Error handling patterns
- Performance optimization
- Code maintainability
- .NET best practices

## 📊 Review Categories

### Security Issues (Critical)
- **SQL Injection**: Unsafe queries
- **XSS**: User input in HTML
- **Auth Bypass**: JWT validation flaws
- **Data Exposure**: Secrets in code
- **CSRF**: Missing tokens

### Code Quality Issues
- **Architecture**: Layer violations, patterns
- **Performance**: N+1 queries, memory leaks
- **Best Practices**: Error handling, logging
- **Maintainability**: Method length, complexity
- **Naming**: Convention violations

### Severity Levels
```csharp
enum IssueSeverity
{
    Info = 1,      // Confidence 25-49
    Warning = 2,    // Confidence 50-79
    Error = 3,     // Confidence 80-99
    Critical = 4    // Confidence 100
}
```

## 🤖 Gemini Integration

### Model Fallback Strategy
```csharp
var models = new[]
{
    "gemini-3.5-flash",   // Primary
    "gemini-3-flash",     // Secondary
    "gemini-3.1-flash-lite" // Fallback
};

var model = models.FirstOrDefault(m => IsAvailable(m)) ?? models.Last();
```

### API Configuration
```json
{
  "generationConfig": {
    "temperature": 0.2,
    "topP": 0.8,
    "maxOutputTokens": 8192,
    "responseMimeType": "application/json"
  }
}
```

## 💬 Comment Format

### Summary Comment
```markdown
## 🤖 Gemini AI Code Review - PR #123

**Status**: ✅ Approved  
**Lines Reviewed**: 456/789  
**Issues Found**: 2 (Warning)

### 🎯 Summary
PR introduces user profile update functionality with proper validation. Minor performance concerns in data fetching.

### ⚠️ Warning Issues
1. 🏗️ Method too long in `UserService.cs:145`
   - Confidence: 75
   - Suggestion: Extract data fetching to separate method
   
2. ⚡ Potential N+1 query in `ProjectController.cs`
   - Confidence: 65
   - Suggestion: Use Include() or fetch data in single query

<sub>Reviewed by Gemini 3.5 Flash | React with 👍 if helpful</sub>
```

### Inline Comments
For file-specific issues:
```markdown
### 📄 `src/Services/UserService.cs`

```csharp
// Line 145
public async Task<UserProfile> GetProfile(int userId)
{
    // Method too long (42 lines)
    // Consider extracting sub-methods
    var user = await _context.Users
        .Include(u => u.Projects)
        .FirstOrDefaultAsync(u => u.Id == userId);
        
    // ... method continues
}
```
```

## 🔐 Error Handling

### API Errors
- Rate limiting: Exponential backoff
- Context limit: Truncate diff
- Model unavailable: Fallback to next model
- Invalid response: Retry with fallback prompt

### GitHub API Errors
- 422 Unprocessable Entity: Fallback to COMMENT
- 403 Forbidden: Check permissions
- Rate limited: Exponential backoff

## 🚀 Deployment

### GitHub Actions Setup
1. Create `.github/workflows/pr-review.yml`
2. Set up GITHUB_TOKEN permissions
3. Configure secret for Gemini API key

### Environment Configuration
```bash
# Required secrets
GEMINI_API_KEY=your_gemini_api_key
BOT_NAME=aivora-pr-bot
```

### Testing Strategy
1. Test with small PRs
2. Verify confidence thresholds
3. Test error scenarios
4. Validate comment formatting

## 📈 Monitoring

### Metrics to Track
- Review execution time
- PR size distribution
- Issue categorization
- Auto-approval rate
- Error rate

### Alerting
- High error rate (>10%)
- Consistent auto-rejections
- Large PR attempts

## 🔗 Dependencies

### NuGet Packages
```xml
<PackageReference Include="GitHub" Version="8.0.0" />
<PackageReference Include="Google.Gemini.Api" Version="1.0.0" />
```

### External Services
- GitHub API (pull requests, comments)
- Google Gemini API (AI analysis)

---

## 📝 Notes

- Bot designed to be conservative (fail on security)
- Review comments in Vietnamese for user experience
- Context-aware about Aivora specific patterns
- Built-in fallback mechanisms
- Respects GitHub API limits

### Future Enhancements
- Integration with Aivora authentication
- Custom rule sets per team
- Historical review analytics
- Training data improvement
