import os
import re
import sys
import json
import subprocess

def run_cmd(args, input_data=None, capture_output=True):
    """Helper to run shell commands safely."""
    try:
        res = subprocess.run(
            args,
            input=input_data,
            capture_output=capture_output,
            text=True,
            check=True
        )
        return res.returncode, res.stdout, res.stderr
    except subprocess.CalledProcessError as e:
        return e.returncode, e.stdout, e.stderr

def main():
    print("Gemini Backend Review Bot Starting...")
    
    # Read environment variables
    gemini_key = os.environ.get("GEMINI_AI_KEY")
    gh_token = os.environ.get("BOT_GITHUB_TOKEN") or os.environ.get("GITHUB_TOKEN")
    pr_number = os.environ.get("PR_NUMBER")
    repo = os.environ.get("REPO")
    head_sha = os.environ.get("HEAD_SHA")
    pr_title = os.environ.get("PR_TITLE", "No title")
    pr_body = os.environ.get("PR_BODY", "No description")
    pr_author = os.environ.get("PR_AUTHOR", "unknown")

    if not gemini_key:
        print("::error::Missing GEMINI_AI_KEY environment variable")
        sys.exit(1)
    if not gh_token:
        print("::error::Missing BOT_GITHUB_TOKEN/GITHUB_TOKEN environment variable")
        sys.exit(1)
    if not pr_number or not repo or not head_sha:
        print("::error::Missing PR metadata (PR_NUMBER, REPO, HEAD_SHA)")
        sys.exit(1)

    # 1. Fetch PR Diff using GitHub CLI/API
    print(f"Fetching diff for PR #{pr_number} in {repo}...")
    code, stdout, stderr = run_cmd([
        "gh", "api",
        f"repos/{repo}/pulls/{pr_number}",
        "-H", "Accept: application/vnd.github.diff"
    ])
    
    if code != 0:
        print(f"::error::Failed to fetch PR diff: {stderr}")
        sys.exit(1)
        
    full_diff = stdout

    # 2. Filter out files that don't need review
    print("Filtering diff files...")
    SKIP_PATTERNS = [
        r'package-lock\.json$',
        r'pnpm-lock\.yaml$',
        r'yarn\.lock$',
        r'\.png$', r'\.jpg$', r'\.jpeg$', r'\.gif$', r'\.svg$', r'\.ico$', r'\.webp$',
        r'\.woff2?$', r'\.ttf$', r'\.eot$',
        r'^bin/', r'^obj/',
        r'\.Designer\.cs$', r'\.g\.cs$',
        r'Migrations/',
        r'\.env\.example$',
        r'\.gitattributes$',
        r'\.gitignore$',
    ]
    
    # Split into per-file diffs
    file_diffs = re.split(r'(?=^diff --git )', full_diff, flags=re.MULTILINE)
    filtered = []
    
    for chunk in file_diffs:
        if not chunk.strip():
            continue
        # Extract file path from "diff --git a/path b/path"
        match = re.match(r'diff --git a/(.*?) b/(.*)', chunk)
        if not match:
            filtered.append(chunk)
            continue
        filepath = match.group(2)
        if any(re.search(p, filepath) for p in SKIP_PATTERNS):
            continue
        filtered.append(chunk)
        
    filtered_diff = ''.join(filtered)
    
    # Check if there is anything left to review
    if not filtered_diff.strip():
        print("No reviewable file changes found. Skipping review.")
        sys.exit(0)

    # Truncate diff if it's too large (~100k chars) to avoid prompt limits
    MAX_CHARS = 100000
    if len(filtered_diff) > MAX_CHARS:
        print(f"Diff size ({len(filtered_diff)} chars) exceeds limit. Truncating...")
        filtered_diff = filtered_diff[:MAX_CHARS] + "\n\n... [diff truncated due to size] ..."

    # 3. Build Prompt for Backend .NET 10
    prompt = """You are a Senior Backend Engineer performing an automated code review for the AIVORA Backend project - an AI Marketplace built with .NET 10 (ASP.NET Core), EF Core 10, PostgreSQL, JWT Auth, and SignalR.

## Project Guidelines

### Architecture & Key Conventions
- **Interface-based DI**: All business logic/services MUST use interface-based Dependency Injection in `Aivora.Services`. Registration should follow: `builder.Services.AddScoped<IService, Service>()`.
- **Namespace Structure**: Services must reside in `Aivora.Services.{ServiceName}` with `IService.cs` and `Service.cs`.
- **Response Wrapper**: All API controllers in `Aivora.api` must wrap their output using the pattern: `{ success: bool, message: string, data: T?, errors?: object }` (or return Microsoft.AspNetCore.Mvc.JsonResult/Ok/ObjectResult containing this structure).
- **Enum Serialization**: All API-facing enums must use string serialization via `[JsonConverter(typeof(JsonStringEnumConverter))]`.
- **Environment Variables**: Configurations must be read from environment variables without hardcoded fallback. Use `__` separator for nested values (e.g. `JwtSettings__Secret`). Fail-fast if missing.
- **Exception Handling**: Rely on the global `ExceptionMiddleware` at the entry point of the pipeline. Do NOT swallow exceptions using empty try-catch blocks.

### Security Focus Areas
1. **SQL Injection**: Ensure raw SQL execution (if any) is parameterized. EF Core LINQ is preferred.
2. **Authorization**: API endpoints in `Aivora.api` must have appropriate authorization attributes (`[Authorize]`, `[Authorize(Policy = "AdminPolicy")]`, etc.) unless explicitly intended to be public (`[AllowAnonymous]`).
3. **Secret Exposure**: Never log, print, or commit secrets, API keys, or `.env` files. Reject hardcoded keys.

### Code Quality & .NET Best Practices
1. **EF Core Performance**: Avoid N+1 query patterns. Use `.Include()` or projection `.Select()` where appropriate.
2. **Async/Await**: Ensure async methods are awaited. Avoid sync-over-async blocking calls like `.Result`, `.GetAwaiter().GetResult()`, or `.Wait()`.
3. **C# 13 NRT**: Ensure Nullable Reference Types are respected. Avoid null reference risks.

## Your Review Task
Review the PR diff below. Focus ONLY on changes introduced in this PR.

### Confidence Scoring
For each issue found, assign a confidence score (0-100):
- 0-49: False positive, style nits, or things linters will catch (ignore these)
- 50-79: Real but minor issues, nits, or non-blocking suggestions
- 80-100: Critical issues, bugs, security vulnerabilities, or violations of project conventions (these block the PR)

### Output Format
Respond with ONLY valid JSON, no markdown fences, no extra text:
{
  "summary": "Brief 1-2 sentence summary of the PR changes",
  "issues": [
    {
      "file": "path/to/file.cs",
      "line": 42,
      "description": "Brief description of the issue in Vietnamese (tiếng Việt)",
      "confidence": 85,
      "category": "bug|security|architecture|performance|best-practice",
      "suggestion": "How to fix it (in Vietnamese)"
    }
  ]
}

If no issues with confidence >= 50 exist, return:
{
  "summary": "Brief summary of changes",
  "issues": []
}"""

    # 4. Call Gemini API
    print("Calling Gemini API...")
    payload = {
        "contents": [{
            "parts": [{
                "text": f"{prompt}\n\n## PR Information\n- **Title**: {pr_title}\n- **Author**: {pr_author}\n- **Description**: {pr_body}\n\n## Diff\n```diff\n{filtered_diff}\n```"
            }]
        }],
        "generationConfig": {
            "temperature": 0.2,
            "topP": 0.8,
            "maxOutputTokens": 8192,
            "responseMimeType": "application/json"
        }
    }

    # Write payload to file
    with open("/tmp/gemini_payload.json", "w") as f:
        json.dump(payload, f)

    # Execute curl request
    code, stdout, stderr = run_cmd([
        "curl", "-s", "-w", "\n%{http_code}",
        f"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={gemini_key}",
        "-H", "Content-Type: application/json",
        "-d", "@/tmp/gemini_payload.json"
    ])

    if code != 0:
        print(f"::error::Gemini API curl call failed: {stderr}")
        sys.exit(1)

    # Parse HTTP response
    response_lines = stdout.splitlines()
    if not response_lines:
        print("::error::Gemini API returned empty response")
        sys.exit(1)
        
    http_code = response_lines[-1].strip()
    body = "\n".join(response_lines[:-1])

    if http_code != "200":
        print(f"::error::Gemini API returned HTTP {http_code}")
        print(body[:500])
        sys.exit(1)

    # Extract JSON text
    try:
        res_json = json.loads(body)
        review_text = res_json["candidates"][0]["content"]["parts"][0]["text"]
    except (json.JSONDecodeError, KeyError, IndexError) as e:
        print(f"::error::Failed to parse Gemini response payload: {e}")
        print(body[:1000])
        sys.exit(1)

    # Parse review JSON
    try:
        review_result = json.loads(review_text)
    except json.JSONDecodeError:
        # Try to strip markdown code blocks if any
        cleaned_text = re.sub(r'^```json\s*|^```\s*|```$', '', review_text.strip(), flags=re.MULTILINE)
        try:
            review_result = json.loads(cleaned_text)
        except json.JSONDecodeError:
            print("::error::Could not parse Gemini output as JSON:")
            print(review_text)
            sys.exit(1)

    print("Gemini Review Result parsed successfully.")
    
    # 5. Process Review Results
    summary = review_result.get("summary", "No summary provided.")
    issues = review_result.get("issues", [])

    high_confidence = [i for i in issues if i.get("confidence", 0) >= 80]
    medium_confidence = [i for i in issues if 50 <= i.get("confidence", 0) < 80]

    # Dismiss old reviews of this bot to prevent blockage
    print("Dismissing old bot reviews...")
    code, stdout, stderr = run_cmd([
        "gh", "api",
        f"repos/{repo}/pulls/{pr_number}/reviews",
        "--jq",
        '[.[] | select(.user.login == "github-actions[bot]" and .state == "CHANGES_REQUESTED") | .id]'
    ])
    
    if code == 0 and stdout.strip():
        try:
            review_ids = json.loads(stdout.strip())
            for rid in review_ids:
                run_cmd([
                    "gh", "api", "--method", "PUT",
                    f"repos/{repo}/pulls/{pr_number}/reviews/{rid}/dismissals",
                    "-f", "message=Superseded by new review run",
                    "-f", "event=DISMISS"
                ])
                print(f"Dismissed old review {rid}")
        except Exception as e:
            print(f"Error dismissing old review: {e}")

    # Build review body
    CATEGORY_ICONS = {
        'bug': '🐛',
        'security': '🔒',
        'architecture': '🏗️',
        'performance': '⚡',
        'best-practice': '💡',
    }

    def format_issues(issue_list, start_idx=1):
        lines = []
        for idx, issue in enumerate(issue_list, start_idx):
            icon = CATEGORY_ICONS.get(issue.get('category', ''), '⚠️')
            conf = issue.get('confidence', 0)
            file_path = issue.get('file', 'unknown')
            line = issue.get('line', 0)
            desc = issue.get('description', 'No description')
            suggestion = issue.get('suggestion', '')
            category = issue.get('category', 'other').upper()

            link = f"https://github.com/{repo}/blob/{head_sha}/{file_path}"
            if line:
                start = max(1, line - 1)
                end = line + 1
                link += f"#L{start}-L{end}"

            lines.append(f"{idx}. {icon} **[{category}]** {desc} (confidence: {conf})")
            lines.append("")
            lines.append(f"   📄 [{file_path}:{line}]({link})")
            if suggestion:
                lines.append("")
                lines.append(f"   💡 **Gợi ý sửa:** {suggestion}")
            lines.append("")
        return lines

    FOOTER = [
        "---",
        "<sub>🤖 Reviewed by Gemini 3.1 Flash Lite | React with 👍 if helpful, 👎 if not</sub>"
    ]

    event = "APPROVE"
    if high_confidence:
        body_lines = [
            "## 🤖 Gemini AI Backend Code Review",
            "",
            f"> {summary}",
            "",
            f"### 🚨 Phát hiện **{len(high_confidence)}** lỗi nghiêm trọng (confidence ≥ 80):",
            "",
        ]
        body_lines.extend(format_issues(high_confidence))
        if medium_confidence:
            body_lines.extend([
                f"### 💬 Ngoài ra có **{len(medium_confidence)}** góp ý nhỏ (confidence 50-79):",
                "",
            ])
            body_lines.extend(format_issues(medium_confidence, len(high_confidence) + 1))
        body_lines.extend(FOOTER)
        event = "REQUEST_CHANGES"
    elif medium_confidence:
        body_lines = [
            "## 🤖 Gemini AI Backend Code Review",
            "",
            f"> {summary}",
            "",
            "✅ **Approved** — Không phát hiện lỗi nghiêm trọng.",
            "",
            f"### 💬 **{len(medium_confidence)}** góp ý nhỏ cho bạn (confidence 50-79):",
            "",
            "*Các góp ý này không bắt buộc sửa để merge, vui lòng xem xét.*",
            "",
        ]
        body_lines.extend(format_issues(medium_confidence))
        body_lines.extend(FOOTER)
        event = "APPROVE"
    else:
        body_lines = [
            "## 🤖 Gemini AI Backend Code Review",
            "",
            f"> {summary}",
            "",
            "✅ Không phát hiện vấn đề nào. Code rất tốt!",
            "",
            "Đã quét: Bugs, Security, Architecture DI, Response wrapper, EF Core Performance.",
            "",
        ]
        body_lines.extend(FOOTER)
        event = "APPROVE"

    review_body = "\n".join(body_lines)

    # 6. Submit Review with Fallback
    print(f"Submitting review as {event}...")
    review_payload = {
        "body": review_body,
        "event": event,
        "commit_id": head_sha
    }
    
    code, stdout, stderr = run_cmd([
        "gh", "api",
        f"repos/{repo}/pulls/{pr_number}/reviews",
        "--method", "POST",
        "--input", "-"
    ], input_data=json.dumps(review_payload))

    if code != 0:
        # Fallback to COMMENT if 422
        if "422" in stderr or "Unprocessable Entity" in stderr:
            print("::warning::Failed to submit review as APPROVE/REQUEST_CHANGES (HTTP 422). Falling back to COMMENT review.")
            
            warning_header = (
                "⚠️ **Note:** Bot attempted to submit this review as "
                f"`{event}` but fell back to `COMMENT` (HTTP 422).\n\n"
                "*Why this happens: GitHub prevents users from approving their own PRs (if using a personal token), "
                "or the GITHUB_TOKEN lacks approval permissions.*"
            )
            fallback_body = f"{warning_header}\n\n---\n\n{review_body}"
            
            fallback_payload = {
                "body": fallback_body,
                "event": "COMMENT",
                "commit_id": head_sha
            }
            
            f_code, f_stdout, f_stderr = run_cmd([
                "gh", "api",
                f"repos/{repo}/pulls/{pr_number}/reviews",
                "--method", "POST",
                "--input", "-"
            ], input_data=json.dumps(fallback_payload))
            
            if f_code == 0:
                print("Review submitted successfully as COMMENT (Fallback).")
            else:
                print(f"::error::Fallback submission failed: {f_stderr}")
                sys.exit(1)
        else:
            print(f"::error::Failed to submit review: {stderr}")
            sys.exit(1)
    else:
        print(f"Review submitted successfully as {event}.")

if __name__ == "__main__":
    main()
