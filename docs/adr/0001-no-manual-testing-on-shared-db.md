# ADR 0001 — Cấm test thủ công trên DB chia sẻ/live

**Trạng thái:** Chấp nhận
**Ngày:** 2026-07-20
**Liên quan:** Issue #149

## Bối cảnh

Find Work (live) từng hiển thị các job post tạo tay trong lúc QA thủ công trực tiếp trên database dev/staging chia sẻ (title kiểu "Final verify job", "Debug job2"; client "Dbg Client", "Test Client2", "DNQA"). Các job này không đến từ `AivoraDataSeeder.cs` — seed data hợp lệ chỉ có 3 job cố định ("Build E-commerce Website", "Mobile App for Fitness Tracking", "Redesign Company Website UI").

`JobPost` hiện không có cột đánh dấu test/seed nào (`IsTest`/`IsSeed`), và không có cơ chế lọc.

## Quyết định

**Không thêm cột `IsTest`/filter kỹ thuật.** Thay vào đó, áp dụng quy tắc vận hành:

> **QA thủ công (click qua UI, tạo data tay để thử flow) chỉ được thực hiện trên môi trường cô lập — local hoặc branch DB riêng. Không bao giờ tạo data tay trực tiếp trên DB dev/staging/production dùng chung.**

Nếu cần dữ liệu mẫu để demo/QA trên môi trường chia sẻ, mở rộng `AivoraDataSeeder.cs` (data có kiểm soát, có thể tái tạo) thay vì thao tác tay qua UI.

## Vì sao không thêm `IsTest`

- Mỗi cột `IsTest` cần migration + đổi mọi query Find Work để filter — chi phí kỹ thuật cho một vấn đề gốc là *kỷ luật quy trình*, không phải thiếu cơ chế lọc.
- Vấn đề tái diễn ngay cả khi có cột: người test vẫn có thể quên set `IsTest=true`.
- Rule vận hành xử lý đúng nguyên nhân gốc: đừng test trên DB chia sẻ.

## Hệ quả

- Demo data (`AivoraDataSeeder.SeedAsync` — admin/client/expert accounts, sample jobs) giờ **không chạy trên môi trường `Production`** (guard tại `Program.cs`, xem #149). User hệ thống `system@aivora.com` + platform wallet vẫn được đảm bảo tồn tại ở **mọi** môi trường kể cả Production, qua `EnsureSystemUserAsync()` chạy vô điều kiện — vì flow trả tiền milestone (`Treasury`) phụ thuộc cứng vào wallet đó.
- Vi phạm rule này (tạo data tay trên shared DB) là nguồn gốc của lần cleanup #149 — lần tới phát hiện sẽ được coi là vi phạm quy trình, không phải bug hệ thống.
