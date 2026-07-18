# Aivora Backend

> Nền tảng kết nối **Client** với **Expert** cho các dự án AI/tech — đăng tin, nhận proposal, quản lý milestone, thanh toán escrow bằng AICoin.

---

## Kiến trúc

```
Aivora.sln
├── Aivora.api          → ASP.NET Core Web API (.NET 10)
├── Aivora.Services     → Business logic, domain services
├── Aivora.Repositories → Entity Framework Core, PostgreSQL, interceptors
└── Aivora.Tests        → Unit & integration tests
```

### Chi tiết các tầng

#### 1. [Aivora.api](./Aivora.api)
Main API Gateway. Xử lý authentication, routing, và real-time communication qua SignalR.
- **Controllers**: Xử lý HTTP requests (20 controllers).
- **Middlewares**: Custom exception handling và security.
- **Extensions**: JWT và Claims helper methods.

> `ChatHub` (SignalR) thực tế nằm ở `Aivora.Services/Hubs/`, không phải `Aivora.api`.

#### 2. [Aivora.Repositories](./Aivora.Repositories)
Tầng truy xuất dữ liệu. Quản lý database entities và persistence sử dụng EF Core.
- **Key Entities**: User, JobPost, Proposal, Project, Wallet, Payment.
- **Features**: Interceptors cho auditable entity tracking, Fluent API configurations.

#### 3. [Aivora.Services](./Aivora.Services)
Tầng logic nghiệp vụ. Chứa core business rules và điều phối dữ liệu giữa API và Repository.
- **Core Services**: AIJobAssistantService, IdentityService, JobService, HiringService, Financial Services (Wallet, Payments, Milestones).

---

## Tech Stack

| Thành phần | Công nghệ |
|---|---|
| Framework | ASP.NET Core 10 (.NET 10) |
| Database | PostgreSQL (EF Core + Npgsql) |
| Auth | JWT Bearer (access token + refresh token) |
| Real-time | SignalR (Chat Hub) |
| File Upload | Cloudinary |
| API Docs | OpenAPI / Scalar (Swagger alternative) |
| Tests | xUnit |

---

## Bắt đầu nhanh

### Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/) (chạy PostgreSQL + app containerized)
- Hoặc PostgreSQL instance riêng

### 1. Clone & cấu hình env

```bash
git clone https://github.com/username/aivora-backend.git
cd aivora-backend
cp .env.example .env
```

Sửa các giá trị trong `.env`:

```env
# Database (connection string PostgreSQL)
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=aivora;Username=postgres;Password=your_password

# JWT
JwtSettings__Secret=your_secret_at_least_32_chars
JwtSettings__Issuer=AivoraApi
JwtSettings__Audience=AivoraClient
JwtSettings__ExpiryInMinutes=1440

# Cloudinary
CloudinaryOptions__CloudName=your_cloud_name
CloudinaryOptions__ApiKey=your_api_key
CloudinaryOptions__ApiSecret=your_api_secret
```

> ⚠️ **Tất cả config đều đọc từ env vars, dùng `__` làm dấu phân cách section.** Nếu thiếu bất kỳ biến bắt buộc nào, app sẽ fail-fast với thông báo rõ ràng. Danh sách đầy đủ (gồm VNPay, Commission, Rate Limit): xem [`docs/ENV.md`](./docs/ENV.md).

### 2. Chạy với Docker Compose

```bash
docker compose up --build
```

API khởi động tại `http://localhost:8080`
API Docs tại `http://localhost:8080/scalar/v1`

### 3. Chạy local (không Docker)

```bash
dotnet restore
dotnet ef database update --project Aivora.Repositories --startup-project Aivora.api
dotnet run --project Aivora.api
```

---

## Cấu trúc API

**Base URL:** `/api/v1`
**Auth:** `Authorization: Bearer <accessToken>`

| Controller | Mô tả | Auth |
|---|---|---|
| `Auth` | Login, Register, Refresh token, Me | Mixed |
| `Users` | Cập nhật thông tin user | Authenticated |
| `Profiles` | Profile Client/Expert (CRUD) | Role-based |
| `Categories` | Danh mục công việc | Public |
| `Skills` | Kỹ năng theo danh mục, thêm/xoá skill cho Expert | Mixed |
| `Jobs` | Đăng tin, publish, cancel, tìm kiếm job | Mixed |
| `Proposals` | Nhận/Gửi/Accept proposal | Role-based |
| `Projects` | Danh sách & chi tiết project | Participant |
| `Milestones` | Fund milestone (escrow), nộp/duyệt deliverable, quản lý milestone steps | Role-based |
| `Disputes` | Mở/xem/giải quyết tranh chấp | Participant / Admin |
| `Wallet` | Số dư ví AICoin, nạp tiền qua VNPay, rút, chuyển ví | Authenticated |
| `Payments` | Lịch sử giao dịch | Authenticated |
| `Reviews` | Đánh giá sau project | Role-based |
| `Messages` | Chat real-time qua SignalR | Authenticated |
| `Notifications` | Thông báo trong app | Authenticated |
| `Media` | Upload/list/xoá file qua Cloudinary | Authenticated |
| `AI` | Trợ lý AI cho job (suggestion, refine) và service generator | Role-based |
| `ExpertVerification` | Nộp/xem bằng chứng xác minh kỹ năng, escalate | Expert / Admin |
| `Admin` | Thống kê, quản lý user, duyệt hồ sơ/verification expert | Admin |
| `Health` | Health check cho hosting (Render) | Public |

> Bàn giao milestone (deliverable) là sub-route của `Milestones`, không có controller riêng.



### SignalR Chat Hub

- **Endpoint:** `/api/v1/chat`
- **Auth:** JWT qua query param `access_token`
- **Client → Server:** `SendMessage(request: {conversationId, content?, attachmentUrl?})`, `JoinConversation(conversationId)`, `LeaveConversation(conversationId)`, `UserTyping(conversationId, isTyping)`, `MarkAsRead(conversationId)`
- **Server → Client:** `ReceiveMessage`, `ReadConfirmation`, `UserTyping`, `JobStatusUpdated`, `NewJobPublished`
- `Error` được đặt trước là reserved, hub hiện chưa emit — xem `docs/ARCHITECTURE.md` mục Known Debt.

---

## Authentication & Authorization

| Role | Quyền |
|---|---|
| `CLIENT` | Đăng job, thuê expert, fund milestone, approve deliverable |
| `EXPERT` | Nhận job, nộp proposal, nộp deliverable, nhận thanh toán |
| `ADMIN` | Quản lý hệ thống, giải quyết dispute |

Flow đăng nhập:
1. `POST /api/v1/auth/login` → nhận `accessToken` + `refreshToken`
2. Gửi `Authorization: Bearer <accessToken>` cho các endpoint protected
3. Khi token hết hạn → `POST /api/v1/auth/refresh-token` → cặp token mới

---

## Quy tắc response

**Thành công:**
```json
{
  "success": true,
  "message": "...",
  "data": { },
  "traceId": "uuid"
}
```

**Lỗi:**
```json
{
  "success": false,
  "message": "Error summary",
  "errors": ["Detail 1", "Detail 2"],
  "traceId": "uuid"
}
```

**Phân trang** (pagination):
```json
"data": {
  "items": [],
  "pageIndex": 1,
  "pageSize": 10,
  "totalItems": 100,
  "totalPages": 10
}
```

---

## Tài liệu chi tiết

- **[Danh mục tài liệu](./docs/README.md)** — Mục lục toàn bộ `docs/`.
- **[Business Flow](./docs/flows/MAINFLOW_v2.md)** — Chi tiết 4 luồng nghiệp vụ chính.
- **[API Reference](./docs/flows/API_BY_FLOW.md)** — Danh sách API theo từng luồng nghiệp vụ.
- **[Kiến trúc hệ thống](./docs/ARCHITECTURE.md)** — Chi tiết layer, service inventory, middleware, config.

---

## Phát triển

### Chạy migration

```bash
dotnet ef migrations add MigrationName --project Aivora.Repositories --startup-project Aivora.api
dotnet ef database update --project Aivora.Repositories --startup-project Aivora.api
```

### Chạy tests

```bash
dotnet test
```
