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
- **Controllers**: Xử lý HTTP requests.
- **Hubs**: SignalR hubs cho real-time chat (`ChatHub`).
- **Middlewares**: Custom exception handling và security.
- **Extensions**: JWT và Claims helper methods.

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

# AI provider
AIProvider__Provider=Mock
AIProvider__ApiKey=
AIProvider__BaseUrl=https://generativelanguage.googleapis.com
AIProvider__Model=gemini-2.5-flash
AIProvider__EnableFallback=true
```

Production must set `AIProvider__Provider=Gemini`, `AIProvider__ApiKey`, and `AIProvider__EnableFallback=false`.

> ⚠️ **Tất cả config đều đọc từ env vars.** Nếu thiếu bất kỳ biến nào, app sẽ fail-fast với thông báo rõ ràng.

### 2. Chạy với Docker Compose

```bash
docker compose up --build
```

API khởi động tại `http://localhost:8080`
API Docs tại `http://localhost:8080/scalar/v1` in Development only.

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
| `Milestones` | Fund milestone (escrow), nộp deliverable, approve | Role-based |
| `Deliverables` | Nộp bàn giao milestone | Expert |
| `Wallet` | Xem số dư ví AICoin | Authenticated |
| `Payments` | Lịch sử giao dịch | Authenticated |
| `Disputes** | Mở/xem/giải quyết tranh chấp | Participant / Admin |
| `Reviews` | Đánh giá sau project | Role-based |
| `Messages` | Chat real-time qua SignalR | Authenticated |
| `Media** | Upload file qua Cloudinary | Authenticated |
| **AI** (AIController) | Trợ lý AI cho job | Role-based |



### SignalR Chat Hub

- **Endpoint:** `/api/v1/chat`
- **Auth:** JWT qua query param `access_token`
- **Client → Server:** `SendMessage(conversationId, content)`
- **Server → Client:** `ReceiveMessage()`, `ReadConfirmation()`, `Error()`

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
  ​"errors": ["Detail 1", "Detail 2"],
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

- **[Business Flow](./docs/flows/MAINFLOW.md)** — Chi tiết 4 luồng nghiệp vụ chính.
- **[API Reference](./docs/flows/API_BY_FLOW.md)** — Danh sách API theo từng luồng nghiệp vụ.
- **[Kiến trúc hệ thống](#kiến-trúc)** — Tổng quan về cấu trúc code.

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

### Verification gates trước refactor

Trước khi refactor logic hoặc kiến trúc, chạy các gate sau từ repo root:

```bash
dotnet restore Aivora.sln
dotnet build Aivora.sln -c Release
dotnet test Aivora.sln -c Release
dotnet format Aivora.sln --verify-no-changes
```

Current automated coverage includes service tests plus API integration tests for authentication, role-based authorization, validation response envelopes, OpenAPI Development-only exposure, and Production AI provider fail-fast config.

Known local caveats:
- Build/test currently pass with `Microsoft.EntityFrameworkCore.Relational` version-conflict warnings from the Npgsql EF provider dependency graph.
- `dotnet format --verify-no-changes` may report pre-existing whitespace issues in older files; fix or format those files before enforcing this gate in CI.

---

## Customization rule notes

- That's it for this task. You can regenerate different READMEs in the future that look different from this one.
