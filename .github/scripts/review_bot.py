import argparse
import base64
import json
import os
import re
import subprocess
import sys
import tempfile

FALLBACK_MODEL_DEFAULT = "gemini-3.1-flash-lite"
GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}"

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

CATEGORY_ICONS = {
    'bug': '🐛',
    'security': '🔒',
    'architecture': '🏗️',
    'performance': '⚡',
    'best-practice': '💡',
}

FOOTER = [
    "---",
    "<sub>🤖 Reviewed by Gemini AI (multi-pass) | React with 👍 if helpful, 👎 if not</sub>",
]

# Hidden marker to find this bot's own "issues found" reviews programmatically,
# independent of the human-facing text/emoji in the body.
ISSUES_FOUND_MARKER = "<!-- aivora-bot-issues-found -->"

COMMON_HEADER = """You are a Senior Backend Engineer performing an automated code review for the AIVORA Backend project - an AI Marketplace built with .NET 10 (ASP.NET Core), EF Core 10, PostgreSQL, JWT Auth, and SignalR.

## Project Guidelines

- **Interface-based DI**: All business logic/services MUST use interface-based Dependency Injection in `Aivora.Services`. Registration should follow: `builder.Services.AddScoped<IService, Service>()`.
- **Namespace Structure**: Services must reside in `Aivora.Services.{ServiceName}` with `IService.cs` and `Service.cs`.
- **Response Wrapper**: All API controllers in `Aivora.api` must wrap their output using the pattern: `{ success: bool, message: string, data: T?, errors?: object }`.
- **Enum Serialization**: All API-facing enums must use string serialization via `[JsonConverter(typeof(JsonStringEnumConverter))]`.
- **Environment Variables**: Configurations must be read from environment variables without hardcoded fallback. Use `__` separator for nested values. Fail-fast if missing.
- **Exception Handling**: Rely on the global `ExceptionMiddleware`. Do NOT swallow exceptions using empty try-catch blocks.
- **Authorization**: API endpoints must have appropriate authorization attributes (`[Authorize]`, `[Authorize(Policy = "...")]`) unless explicitly `[AllowAnonymous]`.
- **EF Core Performance**: Avoid N+1 query patterns. Use `.Include()` or projection `.Select()`.
- **Async/Await**: Avoid sync-over-async blocking calls like `.Result`, `.GetAwaiter().GetResult()`, or `.Wait()`.
- **C# 13 NRT**: Ensure Nullable Reference Types are respected. Avoid null reference risks.
- **Secrets**: Never log, print, or commit secrets, API keys, or `.env` files."""

PASS_INSTRUCTIONS = {
    "claude-md": (
        "## Lens: CLAUDE.md / Convention Compliance\n"
        "Flag ONLY violations of the CLAUDE.md guidelines quoted below (DI pattern, response wrapper, "
        "enum serialization, authorization policy, env var config, exception handling). Ignore bugs, "
        "performance, or style issues not covered by CLAUDE.md."
    ),
    "bug-scan": (
        "## Lens: Bug Scan\n"
        "Shallow scan of the changed lines ONLY (ignore unchanged surrounding code) for obvious bugs: "
        "crashes, incorrect logic, SQL injection, hardcoded secrets, missing authorization checks, N+1 "
        "queries, sync-over-async blocking calls, nullable-reference/null-deref risks. Ignore nitpicks "
        "a linter/compiler would catch."
    ),
    "git-blame": (
        "## Lens: Historical Context (git log)\n"
        "Below is recent git log history for files touched by this PR. Check whether this PR's change "
        "contradicts or regresses behavior/intent established by that history."
    ),
    "pr-history": (
        "## Lens: Related Past PRs\n"
        "Below are review comments from past PRs that touched the same files. Check whether this PR "
        "repeats an issue that was already flagged before."
    ),
    "code-comment": (
        "## Lens: Code Comment Compliance\n"
        "Read the pre-existing comments (TODO, warnings, constraints) visible in the diff context below. "
        "Check whether this PR's change complies with what those comments say."
    ),
}

OUTPUT_FORMAT_INSTRUCTIONS = """## Output Format
Respond with ONLY valid JSON, no markdown fences, no extra text. Only report issues you have at least
moderate confidence in (skip pure style nits or things a linter would catch):
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

If no issues exist, return:
{"summary": "Brief summary of changes", "issues": []}"""

VERIFY_RUBRIC = """## Confidence Rubric
For each issue below, independently verify it and assign a confidence score:
- 0: Not confident at all - false positive, doesn't stand up to scrutiny, or a pre-existing issue.
- 25: Somewhat confident - might be real, might be a false positive.
- 50: Moderately confident - verified as real, but a nitpick or rare in practice.
- 75: Highly confident - verified as real and important; will affect functionality in practice.
- 100: Absolutely certain - direct evidence confirms it, will happen frequently.

Merge/deduplicate issues that describe the same underlying problem (e.g. flagged by multiple passes)
into a single entry before scoring."""


# ---------------------------------------------------------------------------
# Shell / GitHub helpers
# ---------------------------------------------------------------------------

def run_cmd(args, input_data=None):
    """Helper to run shell commands safely."""
    try:
        res = subprocess.run(
            args,
            input=input_data,
            capture_output=True,
            text=True,
            check=True,
        )
        return res.returncode, res.stdout, res.stderr
    except subprocess.CalledProcessError as e:
        return e.returncode, e.stdout, e.stderr


def require_env(name):
    value = os.environ.get(name)
    if not value:
        print(f"::error::Missing {name} environment variable")
        sys.exit(1)
    return value


def write_github_output(name, value):
    path = os.environ.get("GITHUB_OUTPUT")
    if not path:
        print(f"::warning::GITHUB_OUTPUT not set, would have written {name}")
        return
    with open(path, "a", encoding="utf-8") as f:
        f.write(f"{name}={value}\n")


# ---------------------------------------------------------------------------
# Pure helpers (covered by test_review_bot.py)
# ---------------------------------------------------------------------------

def filter_diff(full_diff, skip_patterns=SKIP_PATTERNS):
    """Split a unified diff into per-file chunks, drop files matching skip_patterns."""
    file_diffs = re.split(r'(?=^diff --git )', full_diff, flags=re.MULTILINE)
    filtered = []
    for chunk in file_diffs:
        if not chunk.strip():
            continue
        match = re.match(r'diff --git a/(.*?) b/(.*)', chunk)
        if not match:
            filtered.append(chunk)
            continue
        filepath = match.group(2)
        if any(re.search(p, filepath) for p in skip_patterns):
            continue
        filtered.append(chunk)
    return ''.join(filtered)


def truncate_diff(diff, max_chars=100000):
    if len(diff) <= max_chars:
        return diff
    return diff[:max_chars] + "\n\n... [diff truncated due to size] ..."


def changed_files(diff):
    """Return the list of file paths (b/ side) touched in a unified diff."""
    return re.findall(r'^diff --git a/.*? b/(.*)$', diff, flags=re.MULTILINE)


def strip_json_fences(text):
    return re.sub(r'^```json\s*|^```\s*|```$', '', text.strip(), flags=re.MULTILINE)


def parse_json_response(text):
    try:
        return json.loads(text, strict=False)
    except Exception:
        pass

    cleaned = strip_json_fences(text)
    start = cleaned.find('{')
    if start != -1:
        cleaned = cleaned[start:]
    try:
        obj, _ = json.JSONDecoder(strict=False).raw_decode(cleaned)
        return obj
    except Exception:
        start = text.find('{')
        end = text.rfind('}')
        if start != -1 and end != -1 and end > start:
            return json.loads(text[start:end + 1], strict=False)
        raise


def filter_high_confidence(issues, threshold=80):
    return [i for i in issues if i.get("confidence", 0) >= threshold]


LINKED_ISSUE_RE = re.compile(r'\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s*:?\s*#(\d+)', re.IGNORECASE)


def parse_linked_issue(pr_body):
    """Extract the issue number from a GitHub closing keyword (Closes/Fixes/Resolves #N)."""
    if not pr_body:
        return None
    match = LINKED_ISSUE_RE.search(pr_body)
    return match.group(1) if match else None


def b64_encode(obj_or_str):
    s = obj_or_str if isinstance(obj_or_str, str) else json.dumps(obj_or_str)
    return base64.b64encode(s.encode("utf-8")).decode("ascii")


def b64_decode_str(s):
    return base64.b64decode(s).decode("utf-8")


def b64_decode_json(s):
    return json.loads(b64_decode_str(s))


# ---------------------------------------------------------------------------
# Extra context gatherers (git log / past PRs) - ponytail: bounded, best-effort
# ---------------------------------------------------------------------------

def read_claude_md(path="CLAUDE.md"):
    if not os.path.exists(path):
        return ""
    with open(path, encoding="utf-8") as f:
        return f.read()


def gather_git_blame_context(diff, max_files=10, log_depth=3, max_chars=20000):
    # ponytail: cap files/depth inspected - a PR touching 50+ files isn't reviewable
    # line-by-line anyway, this is best-effort context, not exhaustive history.
    parts = []
    for filepath in changed_files(diff)[:max_files]:
        code, stdout, _ = run_cmd(["git", "log", f"-{log_depth}", "--pretty=format:%h %s", "--", filepath])
        if code == 0 and stdout.strip():
            parts.append(f"### {filepath}\n{stdout}")
    return "\n\n".join(parts)[:max_chars]


PR_REF_RE = re.compile(r'#(\d+)')


def gather_pr_history_context(diff, repo, current_pr_number, max_prs=5, max_files=10, max_chars=20000):
    # ponytail: cap files scanned AND total past PRs found, avoid unbounded git log calls
    # on large PRs that reference no past PR at all.
    pr_numbers = []
    for filepath in changed_files(diff)[:max_files]:
        code, stdout, _ = run_cmd(["git", "log", "--oneline", "--", filepath])
        if code != 0:
            continue
        for match in PR_REF_RE.finditer(stdout):
            num = match.group(1)
            if num != str(current_pr_number) and num not in pr_numbers:
                pr_numbers.append(num)
        if len(pr_numbers) >= max_prs:
            break
    pr_numbers = pr_numbers[:max_prs]

    parts = []
    for num in pr_numbers:
        code, stdout, _ = run_cmd([
            "gh", "api", f"repos/{repo}/pulls/{num}/comments",
            "--jq", "[.[] | {path, body}] | .[:5]",
        ])
        if code == 0 and stdout.strip() and stdout.strip() != "[]":
            parts.append(f"### PR #{num} review comments\n{stdout}")
    return "\n\n".join(parts)[:max_chars]


def gather_linked_issue_context(repo, issue_number, max_chars=4000):
    """Fetch the GitHub issue this PR closes (via Closes/Fixes/Resolves #N), if any."""
    if not issue_number:
        return ""
    code, stdout, _ = run_cmd(["gh", "api", f"repos/{repo}/issues/{issue_number}", "--jq", "{title, body}"])
    if code != 0 or not stdout.strip():
        return ""
    try:
        data = json.loads(stdout)
    except json.JSONDecodeError:
        return ""
    text = f"### Issue #{issue_number}: {data.get('title', '')}\n{data.get('body') or ''}"
    return text[:max_chars]


def gather_previous_review_context(repo, pr_number, max_chars=6000):
    """Fetch this bot's own most recent review on this PR that actually reported issues,
    so the verify pass can tell which findings were fixed since then."""
    code, stdout, _ = run_cmd([
        "gh", "api", f"repos/{repo}/pulls/{pr_number}/reviews",
        "--jq", f'[.[] | select(.user.login == "github-actions[bot]" and '
                f'((.body // "") | contains("{ISSUES_FOUND_MARKER}"))) | .body] | last // empty',
    ])
    if code != 0:
        return ""
    return stdout.strip()[:max_chars]


# ---------------------------------------------------------------------------
# Prompt building
# ---------------------------------------------------------------------------

def build_pr_info(pr_meta, linked_issue=""):
    info = (
        f"## PR Information\n- **Title**: {pr_meta['title']}\n- **Author**: {pr_meta['author']}\n"
        f"- **Description**: {pr_meta['body']}"
    )
    if linked_issue:
        info += f"\n\n## Linked Issue (the PR is meant to resolve this)\n{linked_issue}"
    return info


def build_prompt(pass_name, pr_meta, diff, claude_md="", extra_context="", linked_issue=""):
    parts = [COMMON_HEADER]
    if pass_name == "claude-md":
        parts.append(f"## CLAUDE.md\n```\n{claude_md}\n```")
    parts.append(PASS_INSTRUCTIONS[pass_name])
    if extra_context:
        parts.append(f"## Extra Context\n```\n{extra_context}\n```")
    parts.append(OUTPUT_FORMAT_INSTRUCTIONS)
    parts.append(build_pr_info(pr_meta, linked_issue))
    parts.append(f"## Diff\n```diff\n{diff}\n```")
    return "\n\n".join(parts)


def build_verify_prompt(pr_meta, diff, issues, linked_issue="", previous_review=""):
    parts = [
        "You are independently verifying issues found by earlier automated review passes on a .NET "
        "backend PR for the AIVORA Marketplace.",
        f"## Issues found by earlier review passes\n{json.dumps(issues, ensure_ascii=False, indent=2)}",
    ]
    if previous_review:
        parts.append(
            "## Previous Bot Review On This Same PR\n"
            "This is the bot's own last review comment from an earlier push to this PR. If the current "
            "diff now fixes something it flagged, mention that explicitly in your summary (e.g. \"Đã fix "
            "N/M vấn đề từ lần review trước\"). Do not re-report already-fixed issues.\n"
            f"{previous_review}"
        )
    parts.append(VERIFY_RUBRIC)
    parts.append(OUTPUT_FORMAT_INSTRUCTIONS)
    parts.append(build_pr_info(pr_meta, linked_issue))
    parts.append(f"## Diff (for reference)\n```diff\n{diff}\n```")
    return "\n\n".join(parts)


# ---------------------------------------------------------------------------
# Gemini API call with model fallback
# ---------------------------------------------------------------------------

def _call_gemini_once(prompt, api_key, model):
    payload = {
        "contents": [{"parts": [{"text": prompt}]}],
        "generationConfig": {
            "temperature": 0.2,
            "topP": 0.8,
            "maxOutputTokens": 8192,
            "responseMimeType": "application/json",
        },
    }
    with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False, encoding="utf-8") as f:
        json.dump(payload, f)
        payload_path = f.name

    try:
        url = GEMINI_ENDPOINT.format(model=model, key=api_key)
        code, stdout, stderr = run_cmd([
            "curl", "-s", "-w", "\n%{http_code}", url,
            "-H", "Content-Type: application/json", "-d", f"@{payload_path}",
        ])
    finally:
        os.remove(payload_path)

    if code != 0:
        raise RuntimeError(f"curl failed: {stderr}")

    lines = stdout.splitlines()
    if not lines:
        raise RuntimeError("empty response from Gemini")
    http_code, body = lines[-1].strip(), "\n".join(lines[:-1])
    if http_code != "200":
        raise RuntimeError(f"HTTP {http_code}: {body[:300]}")

    res_json = json.loads(body)
    return res_json["candidates"][0]["content"]["parts"][0]["text"]


def call_gemini(prompt, api_key, primary_model, fallback_model=FALLBACK_MODEL_DEFAULT):
    models_to_try = [primary_model] if primary_model == fallback_model else [primary_model, fallback_model]
    last_err = None
    for model in models_to_try:
        try:
            return _call_gemini_once(prompt, api_key, model)
        except Exception as e:
            print(f"::warning::Gemini call with model {model} failed: {e}")
            last_err = e
    raise RuntimeError(f"Gemini call failed on all attempted models {models_to_try}: {last_err}")


# ---------------------------------------------------------------------------
# Review comment formatting
# ---------------------------------------------------------------------------

def format_issue_lines(idx, issue, repo, head_sha):
    icon = CATEGORY_ICONS.get(issue.get('category', ''), '⚠️')
    conf = issue.get('confidence', 0)
    file_path = issue.get('file', 'unknown')
    line = issue.get('line', 0)
    desc = issue.get('description', 'No description')
    suggestion = issue.get('suggestion', '')
    category = issue.get('category', 'other').upper()

    link = f"https://github.com/{repo}/blob/{head_sha}/{file_path}"
    if line:
        start, end = max(1, line - 1), line + 1
        link += f"#L{start}-L{end}"

    lines = [
        f"{idx}. {icon} **[{category}]** {desc} (confidence: {conf})",
        "",
        f"   📄 [{file_path}:{line}]({link})",
    ]
    if suggestion:
        lines += ["", f"   💡 **Gợi ý sửa:** {suggestion}"]
    lines.append("")
    return lines


def build_review_body(summary, issues, repo, head_sha):
    if issues:
        lines = [
            ISSUES_FOUND_MARKER,
            "## 🤖 Gemini AI Backend Code Review",
            "",
            f"> {summary}",
            "",
            f"### 🚨 Phát hiện **{len(issues)}** vấn đề (confidence ≥ 80):",
            "",
        ]
        for idx, issue in enumerate(issues, 1):
            lines.extend(format_issue_lines(idx, issue, repo, head_sha))
    else:
        lines = [
            "## 🤖 Gemini AI Backend Code Review",
            "",
            f"> {summary}",
            "",
            "✅ Không phát hiện vấn đề nào đáng kể. Code tốt!",
            "",
            "Đã quét: CLAUDE.md compliance, bug scan, git-blame history, PR cũ liên quan, code-comment compliance.",
            "",
        ]
    lines.extend(FOOTER)
    return "\n".join(lines)


def dismiss_stale_reviews(repo, pr_number):
    print("Dismissing old bot reviews...")
    code, stdout, _ = run_cmd([
        "gh", "api", f"repos/{repo}/pulls/{pr_number}/reviews",
        "--jq", '[.[] | select(.user.login == "github-actions[bot]" and .state == "CHANGES_REQUESTED") | .id]',
    ])
    if code != 0 or not stdout.strip():
        return
    try:
        review_ids = json.loads(stdout.strip())
    except json.JSONDecodeError:
        return
    for rid in review_ids:
        run_cmd([
            "gh", "api", "--method", "PUT", f"repos/{repo}/pulls/{pr_number}/reviews/{rid}/dismissals",
            "-f", "message=Superseded by new review run", "-f", "event=DISMISS",
        ])
        print(f"Dismissed old review {rid}")


def submit_review_with_fallback(repo, pr_number, head_sha, body, event):
    print(f"Submitting review as {event}...")
    payload = {"body": body, "event": event, "commit_id": head_sha}
    code, stdout, stderr = run_cmd([
        "gh", "api", f"repos/{repo}/pulls/{pr_number}/reviews", "--method", "POST", "--input", "-",
    ], input_data=json.dumps(payload))

    if code == 0:
        print(f"Review submitted successfully as {event}.")
        return

    if "422" not in stderr and "Unprocessable Entity" not in stderr:
        print(f"::error::Failed to submit review: {stderr}")
        sys.exit(1)

    print("::warning::Failed to submit review as APPROVE/REQUEST_CHANGES (HTTP 422). Falling back to COMMENT.")
    warning = (
        f"⚠️ **Note:** Bot attempted to submit this review as `{event}` but fell back to `COMMENT` (HTTP 422).\n\n"
        "*Why this happens: GitHub prevents users from approving their own PRs, or the token lacks "
        "approval permissions.*"
    )
    fallback_payload = {"body": f"{warning}\n\n---\n\n{body}", "event": "COMMENT", "commit_id": head_sha}
    code, stdout, stderr = run_cmd([
        "gh", "api", f"repos/{repo}/pulls/{pr_number}/reviews", "--method", "POST", "--input", "-",
    ], input_data=json.dumps(fallback_payload))
    if code != 0:
        print(f"::error::Fallback submission failed: {stderr}")
        sys.exit(1)
    print("Review submitted successfully as COMMENT (fallback).")


# ---------------------------------------------------------------------------
# CLI commands
# ---------------------------------------------------------------------------

def load_gemini_config():
    api_key = require_env("GEMINI_AI_KEY")
    model = require_env("MODEL")
    fallback_model = os.environ.get("FALLBACK_MODEL", FALLBACK_MODEL_DEFAULT)
    return api_key, model, fallback_model


def load_pr_meta():
    return {
        "title": os.environ.get("PR_TITLE", "No title"),
        "body": os.environ.get("PR_BODY", "No description"),
        "author": os.environ.get("PR_AUTHOR", "unknown"),
    }


def cmd_prepare(_args):
    repo = require_env("REPO")
    pr_number = require_env("PR_NUMBER")

    print(f"Fetching diff for PR #{pr_number} in {repo}...")
    code, stdout, stderr = run_cmd([
        "gh", "api", f"repos/{repo}/pulls/{pr_number}",
        "-H", "Accept: application/vnd.github.diff",
    ])
    if code != 0:
        print(f"::error::Failed to fetch PR diff: {stderr}")
        sys.exit(1)

    filtered = filter_diff(stdout)
    if not filtered.strip():
        print("No reviewable file changes found. Skipping review.")
        write_github_output("has_changes", "false")
        write_github_output("diff_b64", "")
        return

    truncated = truncate_diff(filtered)
    write_github_output("has_changes", "true")
    with open("pr_diff.txt", "w", encoding="utf-8") as f:
        f.write(b64_encode(truncated))


def cmd_pass(args):
    api_key, model, fallback_model = load_gemini_config()
    with open("pr_diff.txt", "r", encoding="utf-8") as f:
        diff = b64_decode_str(f.read().strip())
    pr_meta = load_pr_meta()
    repo = require_env("REPO")

    claude_md, extra_context = "", ""
    if args.pass_name == "claude-md":
        claude_md = read_claude_md()
    elif args.pass_name == "git-blame":
        extra_context = gather_git_blame_context(diff)
    elif args.pass_name == "pr-history":
        extra_context = gather_pr_history_context(diff, repo, require_env("PR_NUMBER"))

    linked_issue = gather_linked_issue_context(repo, parse_linked_issue(pr_meta["body"]))

    prompt = build_prompt(
        args.pass_name, pr_meta, diff,
        claude_md=claude_md, extra_context=extra_context, linked_issue=linked_issue,
    )

    print(f"Calling Gemini ({model}) for pass '{args.pass_name}'...")
    try:
        response_text = call_gemini(prompt, api_key, model, fallback_model)
        result = parse_json_response(response_text)
        issues = result.get("issues", [])
    except Exception as e:
        print(f"::error::Pass '{args.pass_name}' failed: {e}")
        sys.exit(1)

    print(f"Pass '{args.pass_name}' found {len(issues)} issue(s).")
    with open(f"issues_{args.pass_name}.txt", "w", encoding="utf-8") as f:
        f.write(b64_encode(issues))


ISSUE_ENV_VARS = [
    "ISSUES_CLAUDE_MD_B64",
    "ISSUES_BUG_SCAN_B64",
    "ISSUES_GIT_BLAME_B64",
    "ISSUES_PR_HISTORY_B64",
    "ISSUES_CODE_COMMENT_B64",
]


def cmd_verify(_args):
    api_key, model, fallback_model = load_gemini_config()
    repo = require_env("REPO")
    pr_number = require_env("PR_NUMBER")
    head_sha = require_env("HEAD_SHA")
    
    with open("pr_diff.txt", "r", encoding="utf-8") as f:
        diff = b64_decode_str(f.read().strip())
        
    pr_meta = load_pr_meta()

    all_issues = []
    for pass_name in PASS_INSTRUCTIONS.keys():
        filename = f"issues_{pass_name}.txt"
        if os.path.exists(filename):
            with open(filename, "r", encoding="utf-8") as f:
                raw = f.read().strip()
                if raw:
                    all_issues.extend(b64_decode_json(raw))

    if not all_issues:
        print("No issues reported by any pass. Approving PR.")
        dismiss_stale_reviews(repo, pr_number)
        body = build_review_body("Không có pass nào phát hiện vấn đề.", [], repo, head_sha)
        submit_review_with_fallback(repo, pr_number, head_sha, body, "APPROVE")
        return

    linked_issue = gather_linked_issue_context(repo, parse_linked_issue(pr_meta["body"]))
    previous_review = gather_previous_review_context(repo, pr_number)

    prompt = build_verify_prompt(
        pr_meta, diff, all_issues, linked_issue=linked_issue, previous_review=previous_review,
    )
    print(f"Calling Gemini ({model}) to verify {len(all_issues)} candidate issue(s)...")
    try:
        response_text = call_gemini(prompt, api_key, model, fallback_model)
        result = parse_json_response(response_text)
        summary = result.get("summary", "No summary provided.")
        scored_issues = result.get("issues", [])
    except Exception as e:
        print(f"::error::Verify pass failed: {e}")
        sys.exit(1)

    high_confidence = filter_high_confidence(scored_issues, threshold=80)

    # Dismiss stale reviews only once we have a verified result ready to post -
    # otherwise a crash here would leave the PR with old reviews dismissed and
    # no new one posted.
    dismiss_stale_reviews(repo, pr_number)

    event = "REQUEST_CHANGES" if high_confidence else "APPROVE"
    body = build_review_body(summary, high_confidence, repo, head_sha)
    submit_review_with_fallback(repo, pr_number, head_sha, body, event)


def main():
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="mode", required=True)
    subparsers.add_parser("prepare")
    pass_parser = subparsers.add_parser("pass")
    pass_parser.add_argument("--pass-name", required=True, choices=list(PASS_INSTRUCTIONS.keys()))
    subparsers.add_parser("verify")

    args = parser.parse_args()
    if args.mode == "prepare":
        cmd_prepare(args)
    elif args.mode == "pass":
        cmd_pass(args)
    elif args.mode == "verify":
        cmd_verify(args)


if __name__ == "__main__":
    main()
