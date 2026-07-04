# Gemini PR Review Bot — Upgrade Design (v2)

> **Date:** 2026-07-04
> **Version:** 2.0
> **Status:** Approved
> **Supersedes:** [2024-06-24-gemini-pr-review-bot-design.md](./2024-06-24-gemini-pr-review-bot-design.md) (v1 — chỉ 1 phần được implement thực tế trong `review_bot.py`: single Gemini call, không có 2-stage, không có size gate, không có model fallback chain)

---

## 📋 Overview

Nâng cấp `gemini-review.yml` / `review_bot.py` từ **1 lệnh gọi Gemini duy nhất** (1 prompt gộp mọi loại lỗi) thành một pipeline nhiều pass, mô phỏng theo cách Claude Code's built-in `/review` review một PR thật sự.

### Nguồn tham chiếu

`/review` là command built-in của Claude Code CLI (biên dịch sẵn, không tồn tại dưới dạng file local). Không có plugin nào tên "review" cài trong máy — tài liệu gần nhất, đáng tin cậy nhất mô tả đúng pipeline này là plugin chính thức **`code-review`** của Anthropic (tác giả Boris Cherny, marketplace `claude-plugins-official`, lệnh `/code-review`), được `/review` tự nhận là "anh em" (`for your working diff use /code-review`). Thiết kế dưới đây bám theo pipeline của plugin đó, điều chỉnh cho phù hợp với Gemini API (không có subagent) và đặc thù backend .NET của Aivora.

---

## 🎯 Kiến trúc

### Job graph (GitHub Actions)

Mỗi pass chạy trên **job riêng** (không phải concurrency trong 1 process) để: hiện riêng từng pass trong tab Checks của PR, và tận dụng hành vi mặc định của `needs:` — job phụ thuộc tự skip nếu job nó cần đã fail (= fail-fast miễn phí, không cần code thêm).

```
job: prepare
  → checkout (fetch-depth: 0) + gh api lấy diff + filter file noise + truncate 100k chars
  → outputs: diff (base64), head_sha, pr_title, pr_body, pr_author
        │
        ├──(needs: prepare)──> job: pass-claude-md    [GEMINI_MODEL_LIGHT]
        ├──(needs: prepare)──> job: pass-bug-scan      [GEMINI_MODEL_LIGHT]
        ├──(needs: prepare)──> job: pass-git-blame      [GEMINI_MODEL_LIGHT] (checkout fetch-depth:0 riêng)
        ├──(needs: prepare)──> job: pass-pr-history      [GEMINI_MODEL_LIGHT]
        └──(needs: prepare)──> job: pass-code-comment      [GEMINI_MODEL_LIGHT]
                                                              │
        (needs: [prepare, 5 pass job trên], tự skip nếu bất kỳ job nào fail)
                                                              ▼
                                                job: verify-and-post [GEMINI_MODEL_STRONG]
                                                  → gộp issues từ 5 output
                                                  → verify confidence (1 lệnh gọi, rubric 0/25/50/75/100)
                                                  → lọc bỏ issue <80
                                                  → dismiss stale reviews
                                                  → APPROVE / REQUEST_CHANGES (fallback COMMENT nếu 422)
```

Tổng cộng **6 lệnh gọi Gemini/PR**: 5 pass (song song, model light) + 1 verify (model strong).

### Mỗi pass-job cần tự checkout

GitHub Actions không share filesystem giữa các job (mỗi job = runner riêng). Diff được truyền qua `needs.prepare.outputs.diff` (base64-encode vì job output không nhận nội dung nhiều dòng thô). Pass `pass-git-blame` cần thêm bước `checkout` với `fetch-depth: 0` riêng của nó để chạy `git log`/`git blame`.

---

## 🔍 5 Pass (khớp `/code-review`, nội dung điều chỉnh cho .NET backend)

Không tách lăng kính Security riêng (khác spec v1 — xem mục "Đã bỏ" bên dưới). Security-relevant issues (SQL injection, hardcoded secret, thiếu `[Authorize]`...) được các pass dưới đây bắt qua nội dung prompt, không qua 1 pass riêng.

1. **CLAUDE.md / convention compliance** — interface-based DI, response wrapper `{success, message, data, errors}`, enum `JsonStringEnumConverter`, authorization policy (`[Authorize(Policy=...)]`), env var config (fail-fast, `__` separator), không nuốt exception bằng try-catch rỗng.
2. **Bug scan nông** — chỉ nhìn dòng thay đổi trong diff, không đọc thêm context ngoài PR. Bao gồm cả bug an toàn (SQL injection thô, secret hardcode, N+1 query, sync-over-async).
3. **Git-blame/history** — dùng `git log -p` / `git blame` cho các dòng bị sửa để phát hiện regression hoặc mâu thuẫn với lý do thay đổi trước đó.
4. **PR cũ liên quan cùng file** — `gh api`/`gh pr list --search` tìm PR trước đây từng đổi cùng file, lấy review comment cũ đưa vào prompt để tránh lặp lại vấn đề đã từng bị flag.
5. **Code-comment compliance** — đọc comment sẵn có trong file bị sửa (constraint, TODO, cảnh báo) và kiểm tra PR có tuân thủ không.

---

## ✅ Xác minh confidence (Verify pass)

- **1 lệnh gọi duy nhất** (không phải N lệnh/issue như `/code-review` gốc) — nhận toàn bộ danh sách issue từ 5 pass, kèm rubric:
  - 0: không tin — false positive, không đứng vững khi soi kỹ, hoặc issue có từ trước PR
  - 25: hơi tin — có thể thật nhưng cũng có thể false positive
  - 50: khá tin — xác nhận là thật, nhưng nitpick / ít khi gặp trong thực tế
  - 75: rất tin — xác nhận là thật, quan trọng, sẽ ảnh hưởng chức năng thực tế
  - 100: chắc chắn — bằng chứng trực tiếp xác nhận, sẽ xảy ra thường xuyên
- Prompt yêu cầu **gộp issue trùng lặp/chồng lấn** giữa các pass trước khi chấm (vd bug-scan và CLAUDE.md compliance cùng flag 1 dòng thiếu `[Authorize]`).
- **1 ngưỡng duy nhất: lọc bỏ hết issue <80** (bỏ 3 bậc Info/Warning/Error/Critical của spec v1, và bỏ luôn tầng "50-79 góp ý nhỏ" của code hiện tại).

---

## 🤖 Model routing

| Tier | Env var | Default | Dùng cho |
|---|---|---|---|
| Light | `GEMINI_MODEL_LIGHT` | `gemini-3.1-flash-lite` | 5 pass chính |
| Strong | `GEMINI_MODEL_STRONG` | `gemini-3.5-flash` | Verify pass |
| Fallback | — | `gemini-3.1-flash-lite` | Dùng khi lệnh gọi bằng model chính lỗi/429 (rate limit lớn nhất) |

Cấu hình qua GitHub repo variables (`vars.GEMINI_MODEL_LIGHT`, `vars.GEMINI_MODEL_STRONG`), không hardcode trong script — đổi model không cần sửa code.

Không cần thêm size-gate hay eligibility-check bằng LLM: rate limit free-tier (15rpm/500rpd) đủ dư so với tải thực tế (~2 PR/phút), và việc trải 6 lệnh gọi ra 2 model khác nhau đã tự chia thành 2 bucket rate-limit riêng.

---

## 💬 Hành động review

Giữ nguyên hành vi hiện tại — **khác** `/code-review` (vốn chỉ post 1 COMMENT trích dẫn, không approve/block):

- Còn issue ≥80 sau verify → `REQUEST_CHANGES`
- Không còn issue nào ≥80 → `APPROVE`
- Dismiss review cũ của bot khi có commit mới (giữ nguyên logic hiện tại)
- Fallback sang `COMMENT` nếu submit review gặp HTTP 422 (giữ nguyên logic hiện tại)
- Comment trích dẫn theo file + line + link `blob/{head_sha}/...#L{start}-L{end}` (giữ nguyên, đã có sẵn trong code hiện tại)

---

## ❌ Đã bỏ so với spec v1 (2024-06-24)

| Mục trong spec v1 | Lý do bỏ |
|---|---|
| Stage Security fail-fast riêng (return ngay nếu phát hiện critical security) | Chọn khớp hoàn toàn `/code-review` — security gộp vào bug-scan + CLAUDE.md compliance thay vì 1 pass riêng |
| Size gate (skip PR >1000 dòng) | Rate limit free-tier đủ dư; chưa từng được implement trong code hiện tại nên không phải regression |
| 5 bậc confidence (0-25/26-49/50-79/80-99/100) với 2 nhánh hành động | Đổi thành 1 ngưỡng 80 duy nhất, khớp `/code-review` |
| Model fallback list 3 model thử lần lượt theo *availability* | Đổi thành model routing theo *tier độ nặng tác vụ* (light/strong) + 1 fallback chung, phù hợp hơn với multi-pass architecture mới |

---

## 📁 Thay đổi file

- `.github/workflows/gemini-review.yml` — viết lại thành nhiều job (`prepare`, 5 pass job, `verify-and-post`) thay vì 1 job gọi 1 script.
- `.github/scripts/review_bot.py` — tách thành script nhận `--pass=<name>` để mỗi pass-job gọi với tham số khác nhau, cộng thêm mode `--verify` cho job cuối.

Chi tiết code (prompt text từng pass, cách encode/decode diff qua job output, cách bound số PR cũ lấy về ở pass #4) để `/implement` quyết định khi viết code — đây là spec kiến trúc, không phải spec dòng-lệnh.

- `.github/scripts/test_review_bot.py` — self-check cho các hàm thuần (filter/truncate diff, parse JSON, lọc confidence), chạy như 1 step trong job `prepare` trước khi làm gì khác.

## 📝 Ghi chú sau khi implement (lệch có chủ đích so với spec)

- **`head_sha`/`pr_title`/`pr_body`/`pr_author` không đi qua output của `prepare`** như mô tả ở job graph — các field này lấy thẳng từ `github.event.pull_request.*` ở env cấp workflow, vì chúng là dữ liệu tĩnh sẵn có, không cần tính toán/fetch gì. Đơn giản hơn bản spec ban đầu, không mất thông tin gì.
- **Không phải lúc nào cũng đúng 6 lệnh gọi Gemini/PR**: nếu cả 5 pass không tìm thấy issue nào, `cmd_verify` bỏ qua lệnh verify và approve thẳng (5 lệnh, không phải 6) — tránh gọi Gemini để verify một danh sách rỗng.
