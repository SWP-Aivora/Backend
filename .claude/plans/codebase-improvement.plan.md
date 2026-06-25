# Plan: Codebase Improvement từ PR #14, #25

**Source PRs**: #14 (AI validation), #25 (API alignment)
**Complexity**: Medium
**Branch Strategy**: 7 branches độc lập, mỗi branch ≤ 500 lines diff, checkout về main sau mỗi branch

---

## Summary

Tận dụng code từ PR #14 (input validation hardening) và PR #25 (API contract alignment, bug fixes, integration tests) để cải thiện codebase. Mỗi branch tập trung một mục đích duy nhất, đủ nhỏ để Gemini PR Review Bot có thể review hiệu quả (≤ 1000 lines, bỏ qua generated/migration/test noise patterns).

---

## Patterns to Mirror

| Category | Source | Pattern |
|---|---|---|
| Naming | `WalletService/Service.cs` | `MapToResponse()` pattern, Vietnamese log messages |
| Errors | `Exceptions/DomainException.cs` | `ValidationException`, `NotFoundException`, `UnauthorizedException` |
| Data access | `AivoraDbContext` (toàn codebase) | Direct `_dbContext` với `BeginTransactionAsync()` |
| Tests | `Aivora.Tests/Services/*Tests.cs` | xUnit + FluentAssertions + EF Core InMemory |
| API response | `Models/ApiResponse.cs` | `ApiResponseFactory.SuccessResponse()` wrapper |

---

## Branch 1: `fix/wallet-enum-and-validation`

**Mục đích**: Sửa enum thiếu + validation hardening
**Lines ước tính**: ~80 lines

| File | Action | Why |
|---|---|---|
| `Aivora.Repositories/Enums/WalletTransactionType.cs` | UPDATE | Thêm `WITHDRAWAL_REQUEST`, `WITHDRAWAL_COMPLETED` — enum thiếu gây lỗi runtime |
| `Aivora.Services/DeliverableService/Service.cs` | UPDATE | Validate ít nhất 1 evidence field; fix MapToResponse trả về FileUrl/DemoUrl/SourceCodeUrl/Note thay vì null |
| `Aivora.Services/ReviewService/Service.cs` | UPDATE | Validate Rating 1-5 cho CommunicationRating, QualityRating, DeadlineRating, RequirementClarityRating |

**Validate**: `dotnet build && dotnet test --filter "FullyQualifiedName~DeliverableService|ReviewService|WalletService"`

---

## Branch 2: `fix/terminology-simulated-transfer`

**Mục đích**: Đồng bộ thuật ngữ "escrow" → "simulated direct transfer"
**Lines ước tính**: ~100 lines

| File | Action | Why |
|---|---|---|
| `Aivora.Services/AdminService/IAdminService.cs` | UPDATE | `TotalEscrowAmount` → `TotalSimulatedTransferAmount` |
| `Aivora.Services/AdminService/AdminService.cs` | UPDATE | Đồng bộ rename |
| `Aivora.Services/DisputeService/Service.cs` | UPDATE | Bỏ `FreezeFundsAsync()` call, update error message |
| `Aivora.Services/Treasury/ITreasury.cs` | UPDATE | Doc comments: "ký quỹ" → "ghi nhận trực tiếp mô phỏng" |
| `Aivora.Services/Treasury/Treasury.cs` | UPDATE | Log messages: "escrow/fund" → "direct transfer" |

**Validate**: `dotnet build && dotnet test --filter "FullyQualifiedName~AdminService|DisputeService|Treasury"`

---

## Branch 3: `feat/api-response-business-context`

**Mục đích**: Thêm context fields vào API response để frontend hiểu rõ business meaning
**Lines ước tính**: ~60 lines

| File | Action | Why |
|---|---|---|
| `Aivora.Services/MilestoneService/Response.cs` | UPDATE | Thêm `BusinessMeaning` + `Explanation` cho PaymentInfo |
| `Aivora.Services/WalletService/Response.cs` | UPDATE | Thêm `BalanceType`, `Explanation` cho WalletResponse; `BusinessMeaning` + `Explanation` cho TransactionResponse |

**Validate**: `dotnet build && dotnet test --filter "FullyQualifiedName~MilestoneService|WalletService"`

---

## Branch 4: `feat/job-ai-input-validation`

**Mục đích**: Input hardening cho Job và AI Assistant services (từ PR #14)
**Lines ước tính**: ~250 lines

| File | Action | Why |
|---|---|---|
| `Aivora.Services/JobService/Service.cs` | UPDATE | Thêm `ValidateJobFields()`, `NormalizeGuidList()`, `NormalizeMilestones()` |
| `Aivora.Services/AIJobAssistantService/Service.cs` | UPDATE | Thêm `ValidateSuggestionShape()`, `ValidateBudgetAndTimeline()`, `NormalizeMilestones()`, `NormalizeGuidList()` |
| `Aivora.Tests/Services/AIJobAssistantServiceTests.cs` | UPDATE | Thêm test: reject invalid budget range, validate milestones |
| `Aivora.Tests/Services/JobServiceTests.cs` | CREATE | Nếu chưa có, tạo test cho job validation |

**Validate**: `dotnet test --filter "FullyQualifiedName~AIJobAssistantServiceTests|JobServiceTests"`

---

## Branch 5: `test/api-contract-infrastructure`

**Mục đích**: Thiết lập integration test infrastructure (từ PR #25)
**Lines ước tính**: ~450 lines

| File | Action | Why |
|---|---|---|
| `Aivora.Tests/Aivora.Tests.csproj` | UPDATE | Thêm `Microsoft.AspNetCore.Mvc.Testing` + project ref đến `Aivora.api` |
| `Aivora.Tests/ApiContract/ApiContractTestFactory.cs` | CREATE | `WebApplicationFactory<Program>` với InMemory DB + FakeMedia |
| `Aivora.Tests/ApiContract/ApiContractClient.cs` | CREATE | HTTP client helper: Login, GET/POST/PUT/PATCH/DELETE/Multipart với auth |
| `Aivora.Tests/ApiContract/ApiContractTestData.cs` | CREATE | Seed data: users, profiles, wallets, category, skills với fixed GUIDs |
| `Aivora.Tests/ApiContract/FakeMediaService.cs` | CREATE | Mock Cloudinary upload cho tests |
| `Aivora.Tests/ApiContract/ApiVerificationResult.cs` | CREATE | Tracker + JSON export cho API contract verification |

**Validate**: `dotnet build` (chưa có test chạy, chỉ setup infrastructure)

---

## Branch 6: `test/api-business-flow-tests`

**Mục đích**: API contract verification tests cho các business flow (từ PR #25)
**Lines ước tính**: ~500 lines

| File | Action | Why |
|---|---|---|
| `Aivora.Tests/ApiContract/Flow1JobAndAiApiTests.cs` | CREATE | 16 endpoints: AI suggestion → refine → accept/reject → job CRUD → recommendations |
| `Aivora.Tests/ApiContract/Flow2ProposalProjectApiTests.cs` | CREATE | 10 endpoints: proposal submit → shortlist → withdraw → reject → accept → project |
| `Aivora.Tests/ApiContract/Flow3MilestonePaymentDeliverableApiTests.cs` | CREATE | Milestone funding → deliverable submit → approve/revision → payment release |
| `Aivora.Tests/ApiContract/Flow3DisputeReviewApiTests.cs` | CREATE | Dispute open → evidence → resolve + review create → read |
| `Aivora.Tests/ApiContract/SupportingApiTests.cs` | CREATE | Category, skill, wallet, admin, notification, media endpoints |

**Validate**: `dotnet test --filter "FullyQualifiedName~ApiContract"`

---

## Branch 7: `test/update-existing-unit-tests`

**Mục đích**: Cập nhật unit tests hiện có để khớp với code changes
**Lines ước tính**: ~150 lines

| File | Action | Why |
|---|---|---|
| `Aivora.Tests/Services/AdminServiceTests.cs` | UPDATE | Đồng bộ với rename TotalEscrowAmount |
| `Aivora.Tests/Services/DisputeServiceTests.cs` | UPDATE | Đồng bộ với bỏ FreezeFundsAsync |
| `Aivora.Tests/Services/E2EBusinessFlowTests.cs` | UPDATE | Đồng bộ terminology |
| `Aivora.Tests/Services/WalletServiceTests.cs` | UPDATE | Thêm test cho enum mới nếu cần |

**Validate**: `dotnet test` (toàn bộ test suite)

---

## Branch Execution Order & Dependencies

```
main
  │
  ├─► Branch 1: fix/wallet-enum-and-validation          (độc lập)
  ├─► Branch 2: fix/terminology-simulated-transfer       (độc lập)
  ├─► Branch 3: feat/api-response-business-context       (độc lập)
  ├─► Branch 4: feat/job-ai-input-validation             (độc lập)
  ├─► Branch 5: test/api-contract-infrastructure         (độc lập)
  ├─► Branch 6: test/api-business-flow-tests             (phụ thuộc Branch 5)
  └─► Branch 7: test/update-existing-unit-tests          (phụ thuộc Branch 1-4)
```

**Quy trình mỗi branch**:
```bash
git checkout main
git pull origin main
git checkout -b <branch-name>
# ... thực hiện changes ...
dotnet build
dotnet test
git add .
git commit -m "<type>: <description>"
git push -u origin <branch-name>
# Tạo PR, chờ review bot + merge
git checkout main
git pull origin main
# → Tiếp tục branch tiếp theo
```

---

## PR Size Compliance (Gemini Review Bot)

| Branch | Files | Lines ước tính | Pass Size Gate? |
|---|---|---|---|
| 1: wallet-enum-validation | 3 | ~80 | ✅ |
| 2: terminology | 5 | ~100 | ✅ |
| 3: response-context | 2 | ~60 | ✅ |
| 4: input-validation | 4 | ~250 | ✅ |
| 5: test-infrastructure | 6 | ~450 | ✅ |
| 6: flow-tests | 5 | ~500 | ✅ |
| 7: unit-tests | 4 | ~150 | ✅ |

Tất cả branch đều dưới 1000 lines — đảm bảo Gemini PR Review Bot không skip.

---

## Validation

```bash
# Build toàn solution
dotnet build

# Format check
dotnet format Aivora.sln --verify-no-changes --verbosity minimal

# Test toàn bộ
dotnet test

# Riêng integration tests (Branch 5+6) cần PostgreSQL local:
# Host=localhost;Port=5432;Database=aivora_api_contract_tests;Username=postgres;Password=postgres
```

---

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Branch 6 integration tests fail do thiếu PostgreSQL | MEDIUM | Đảm bảo PostgreSQL local đang chạy trước khi test |
| Conflict giữa các branch khi merge tuần tự | LOW | Mỗi branch touch các file khác nhau, ít overlap |
| Test coupling giữa Branch 5 và 6 | MEDIUM | Branch 5 chỉ setup, Branch 6 mới có test thực — merge riêng |
| VNPay feature branch conflict | LOW | Checkout main (đã có VNPay) trước khi tạo branch mới |

---

## Acceptance

- [ ] Tất cả 7 branch merged vào main
- [ ] `dotnet build` pass
- [ ] `dotnet test` pass (toàn bộ)
- [ ] `dotnet format` pass
- [ ] Gemini PR Review Bot approve từng PR
- [ ] Không branch nào > 1000 lines diff
