# Aivora Backend — Documentation Index

> Tài liệu kỹ thuật cho `Aivora-Backend`. Bắt đầu ở đây để tìm đúng file.

| Tôi muốn... | Đọc |
|---|---|
| Hiểu tổng quan kiến trúc, layer, service inventory, middleware, config | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| Biết cần set biến môi trường nào | [`ENV.md`](ENV.md) |
| Setup máy dev, chạy test, quy tắc code style, PR checklist | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| Biết dữ liệu seed có sẵn (tài khoản demo, job, project mẫu) | [`SEED_DATA.md`](SEED_DATA.md) |
| Hiểu 4 luồng nghiệp vụ chính (business flow) | [`flows/MAINFLOW_v2.md`](flows/MAINFLOW_v2.md) |
| Tra cứu chi tiết từng API endpoint (request/response) | [`flows/API_BY_FLOW.md`](flows/API_BY_FLOW.md) |
| Xem schema database | `Aivora.Repositories/Data/Migrations/` (EF Core migrations — nguồn sự thật, không có file SQL dump riêng) |
| Hiểu domain model / ubiquitous language (DDD) | [`../CONTEXT.md`](../CONTEXT.md) |

---

## Cấu trúc thư mục `docs/`

```
docs/
├── README.md          ← file này
├── ARCHITECTURE.md     ← kiến trúc hệ thống
├── ENV.md               ← biến môi trường
├── CONTRIBUTING.md      ← dev setup, test, code style, PR checklist
├── SEED_DATA.md         ← dữ liệu seed / tài khoản demo
└── flows/
    ├── MAINFLOW_v2.md    ← 4 luồng nghiệp vụ chính (nguồn sự thật)
    └── API_BY_FLOW.md    ← API reference chi tiết theo từng luồng
```

Các file khác ở root project (`../README.md`, `../AGENTS.md`, `../CLAUDE.md`) là context cho agent/dev, không lặp lại nội dung ở đây.

---

## Nguyên tắc giữ docs không drift

1. Mọi thay đổi API/entity/enum **phải** cập nhật `ARCHITECTURE.md` + `flows/API_BY_FLOW.md` trong cùng PR.
2. `flows/MAINFLOW_v2.md` là nguồn sự thật cho tên trạng thái/nghiệp vụ — các doc khác tham chiếu tới đây, không định nghĩa lại.
3. Không tạo file doc mới ở `docs/` mà không link từ bảng trên — file mồ côi sẽ bị bỏ sót lần rà soát sau.
