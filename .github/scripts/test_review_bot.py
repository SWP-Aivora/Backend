"""Self-check for the pure/deterministic helpers in review_bot.py.

Run with: python3 .github/scripts/test_review_bot.py
No pytest/unittest fixtures - plain assert functions, stdlib only.
"""

from review_bot import (
    b64_decode_json,
    b64_decode_str,
    b64_encode,
    changed_files,
    filter_diff,
    filter_high_confidence,
    parse_json_response,
    parse_linked_issue,
    truncate_diff,
)


def test_filter_diff_drops_skip_patterns():
    diff = (
        "diff --git a/src/Foo.cs b/src/Foo.cs\n+ real change\n"
        "diff --git a/bin/Debug/Foo.dll b/bin/Debug/Foo.dll\n+ binary noise\n"
        "diff --git a/package-lock.json b/package-lock.json\n+ lockfile noise\n"
        "diff --git a/Migrations/20260101_Init.cs b/Migrations/20260101_Init.cs\n+ ef migration noise\n"
    )
    result = filter_diff(diff)
    assert "src/Foo.cs" in result
    assert "bin/Debug" not in result
    assert "package-lock.json" not in result
    assert "Migrations/" not in result


def test_truncate_diff_under_limit_unchanged():
    diff = "short diff"
    assert truncate_diff(diff, max_chars=100) == diff


def test_truncate_diff_over_limit_truncates():
    diff = "x" * 200
    result = truncate_diff(diff, max_chars=100)
    assert result.startswith("x" * 100)
    assert "truncated" in result


def test_parse_json_response_plain():
    text = '{"summary": "ok", "issues": []}'
    assert parse_json_response(text) == {"summary": "ok", "issues": []}


def test_parse_json_response_with_markdown_fence():
    text = '```json\n{"summary": "ok", "issues": []}\n```'
    assert parse_json_response(text) == {"summary": "ok", "issues": []}


def test_parse_json_response_with_trailing_extra_data():
    text = '{"summary": "ok", "issues": []}\n{"summary": "duplicate", "issues": []}'
    assert parse_json_response(text) == {"summary": "ok", "issues": []}


def test_filter_high_confidence_threshold():
    issues = [{"confidence": 79}, {"confidence": 80}, {"confidence": 100}]
    assert filter_high_confidence(issues, threshold=80) == [
        {"confidence": 80},
        {"confidence": 100},
    ]


def test_changed_files_extracts_paths():
    diff = (
        "diff --git a/src/Foo.cs b/src/Foo.cs\n+x\n"
        "diff --git a/src/Bar.cs b/src/Bar.cs\n+y\n"
    )
    assert changed_files(diff) == ["src/Foo.cs", "src/Bar.cs"]


def test_parse_linked_issue_variants():
    assert parse_linked_issue("Closes #42") == "42"
    assert parse_linked_issue("This fixes #7 for real") == "7"
    assert parse_linked_issue("Resolved: #123") == "123"
    assert parse_linked_issue("no issue reference here") is None
    assert parse_linked_issue("") is None
    assert parse_linked_issue(None) is None


def test_b64_roundtrip():
    issues = [{"file": "a.cs", "confidence": 90}]
    assert b64_decode_json(b64_encode(issues)) == issues
    assert b64_decode_str(b64_encode("hello")) == "hello"


def run_all():
    tests = [v for k, v in globals().items() if k.startswith("test_")]
    for test in tests:
        test()
        print(f"ok  {test.__name__}")
    print(f"\n{len(tests)} passed")


if __name__ == "__main__":
    run_all()
