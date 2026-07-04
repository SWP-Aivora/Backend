# Báo cáo đối chiếu đồng bộ Backend ↔ Frontend

**Ngày kiểm tra:** 2026-07-03, cập nhật 2026-07-04
**Phạm vi:** `Aivora-Backend` (toàn bộ Controllers/Services) đối chiếu với `Aivora-Frontend` (toàn bộ service layer gọi API + SignalR).
**Phương pháp:** Đọc trực tiếp source code của cả hai repo (controllers, DTO, service files) — không dựa vào file OpenAPI spec cũ (`Aivoraapi v1.json` ở FE), vì file đó đã lỗi thời (xem mục 6).

**Kết luận cập nhật (07-04):** Không phát hiện tính năng nào FE cần mà BE thiếu. Toàn bộ endpoint FE đang gọi đều khớp route BE (trừ 1 route admin cũ, xem mục 2.1 — BE đã sửa xong, còn nợ ở phía FE). Các việc còn lại đều là FE chưa tích hợp UI cho tính năng BE đã có sẵn.

---

## 0. Cập nhật mới nhất từ Backend (PR #87 — 2026-07-04, chưa merge)

Phiên làm việc riêng ở Aivora-Backend đã sửa 3 vấn đề liên quan trực tiếp tới các mục ở dưới. **FE nên đọc mục này trước khi làm theo mục 7**, vì nó thay đổi trạng thái của dòng "Xóa file đã upload" ở mục 3.

| # | Vấn đề đã sửa | Chi tiết | Ảnh hưởng tới FE |
|---|---|---|---|
| 1 | `Skills` luôn rỗng trong response profile expert | `GET /profiles/expert` và `GET /profiles/expert/{expertId}` giờ trả đúng `skills: [{skillId, skillName, proficiencyLevel}]` (trước đây luôn `null`/rỗng dù expert đã có skill trong DB) | Màn hình profile expert + verification giờ hiển thị skill thật, không cần workaround |
| 2 | Không có endpoint list media đã upload | Thêm **`GET /api/v1/media`** (mới, không cần param) — trả media của user hiện tại (theo JWT): `[{url, publicId, format, bytes, createdAt}]`. Cover cả ảnh lẫn file khác (PDF certificate cũng hiện đúng, `format: "pdf"`) | Điều kiện cần để làm UI "xóa file đã upload" (mục 3) — FE giờ gọi được `GET /media` để biết `publicId` cần xóa |
| 3 | `DELETE /media/{publicId}` trước đây chỉ ADMIN xóa được | Giờ **user thường (CLIENT/EXPERT) tự xóa được media của chính mình**; ADMIN vẫn xóa được bất kỳ media nào. Xóa media không phải của mình → `401` kèm message rõ ràng | UI "xóa file đã upload" (mục 3) giờ làm được cho user thường, không chỉ admin |

**Lưu ý kỹ thuật khi FE implement UI xóa media:**
- `publicId` Cloudinary trả về **có thể chứa dấu `/`** (VD: `aivora/avatars/xyz123`). Khi build URL gọi `DELETE /api/v1/media/{publicId}`, **không encode** dấu `/` đó (không dùng `encodeURIComponent` cho cả chuỗi) — route BE nhận nguyên publicId (kể cả `/`) làm 1 path parameter (catch-all).
- `GET /media` chưa có phân trang — giới hạn cứng 500 item/loại (image + raw) ở BE, đủ dùng cho user thường, chưa cần lo UI phân trang.

## 1. Đã sửa trong phiên làm việc này

| # | Vấn đề | File | Trạng thái |
|---|---|---|---|
| 1 | `AdminProjectDisputesPage.tsx` gọi `disputeService.getEvidence()` → `GET /disputes/{id}/evidence` — route **không tồn tại** ở BE (BE chỉ trả `evidence` lồng sẵn trong `GET /disputes/{id}`) | `src/features/admin/pages/AdminProjectDisputesPage.tsx`, `src/features/disputes/services.ts` | ✅ Đã sửa — dùng `evidences` có sẵn từ `getDisputeById`, xóa hàm `getEvidence` và test liên quan |
| 2 | `uploadContract`/`confirmContract` gọi `/contracts/upload`, `/contracts/{id}/confirm` — **không có `ContractController` nào ở BE**, và cũng không có UI nào gọi 2 hàm này (code chết) | `src/features/projects/services.ts` | ✅ Đã xóa |

## 2. Vấn đề còn tồn đọng

### 2.1. ✅ BE đã sửa xong (PR #85) — còn nợ ở phía FE

- BE đã bổ sung đầy đủ theo hướng (b) đề xuất trước đó:
  - `GET /admin/expert-profile-updates/{id}` (endpoint chi tiết theo id, **mới**)
  - `PUT /admin/expert-profile-updates/{id}/review` (body `{isApproved, rejectionReason?}`)
  - `ExpertProfileUpdateResponse` giờ có đủ `ExpertId, FullName, Email, AvatarUrl` + cặp giá trị `Current*` (`CurrentTitle, CurrentBio, CurrentHourlyRate, CurrentExperienceYears`) để so sánh trước/sau (`Aivora.Services/AdminService/IAdminService.cs:48-63`).
- **FE vẫn chưa cập nhật:** `admin/services.ts` + `shared/constants/index.ts` vẫn gọi route cũ không tồn tại — `GET admin/expert-reviews/{id}` và `POST admin/expert-reviews/{id}/process`. Nghĩa là màn hình admin duyệt yêu cầu cập nhật hồ sơ expert **hiện đang gọi 404 ở thực tế**, chỉ chạy được nhờ dữ liệu preview/mock (`previewAdminService.ts`).
- **Việc cần làm (FE, không phải BE):** đổi `ADMIN.EXPERT_REVIEW_DETAIL`/`PROCESS_EXPERT_REVIEW` sang `admin/expert-profile-updates/{id}` (GET) và `admin/expert-profile-updates/{id}/review` (PUT, method đổi từ POST → PUT, body đổi từ `{status, note}` sang `{isApproved, rejectionReason}`), đồng thời cập nhật type `ExpertReviewDetail` cho khớp field mới (bỏ `portfolio/skillsComparison` không có ở BE, hoặc giữ BE bổ sung thêm nếu nghiệp vụ thực sự cần).

### 2.2. ✅ BE đã có realtime "JobStatusUpdated" — FE có hook chờ sẵn nhưng chưa bật

- PR #84 thêm `RealtimeService` (`Aivora.Services/RealtimeService/`), emit event `JobStatusUpdated` qua hub `/api/v1/chat` khi: tạo job (OPEN), hủy job (CANCELLED), nhận đề xuất (IN_PROGRESS), dự án hoàn thành (COMPLETED).
- Đã xác minh `Clients.User(userId)` hoạt động đúng: không có `IUserIdProvider` custom → SignalR dùng `DefaultUserIdProvider` (khớp `ClaimTypes.NameIdentifier`), JWT của BE set đúng claim này (`JwtExtensions.cs:38`), và hub nhận token qua query string `access_token` cho WebSocket (`OnMessageReceived`) — chuỗi hoạt động đúng, không có gap.
- FE đã có sẵn `useJobStatusUpdates.ts` + `chatService.onJobStatusUpdate` lắng nghe đúng event `JobStatusUpdated`, nhưng có comment `ponytail:` ghi rõ "backend has no JobStatusUpdated hub event yet" — **comment này giờ đã lỗi thời**, vì hook không tự mở connection (`chatService.connect()`), chỉ ăn theo connection có sẵn khi user đang mở khung chat. Cần FE tự gọi `connect()` (hoặc tái dùng connection global) để nhận được event này ở các trang không mở chat (VD: danh sách job của client).

### 2.3. Hai luồng AI "refine" trùng lặp ở Backend (không phải bug, nhưng nên dọn)

- `POST /ai/job-assistant/{id}/refine` (sửa `AIJobSuggestion` — dùng `AIJobAssistantService.IAIJobRefinementProvider`) và `POST /ai/jobs/{jobId}/refine` (sửa `Job` đã tạo — dùng `AIJobRefinementService.IAIJobRefinementProvider`) là **2 interface/implementation khác nhau nhưng gần như giống hệt nhau**, đăng ký DI riêng biệt. FE có gọi cả hai, khớp đúng route, không phải bug — nhưng là nợ kỹ thuật ở BE nên cân nhắc gộp lại.

### 2.2. Hai luồng AI "refine" trùng lặp ở Backend (không phải bug, nhưng nên dọn)

- `POST /ai/job-assistant/{id}/refine` (sửa `AIJobSuggestion` — dùng `AIJobAssistantService.IAIJobRefinementProvider`) và `POST /ai/jobs/{jobId}/refine` (sửa `Job` đã tạo — dùng `AIJobRefinementService.IAIJobRefinementProvider`) là **2 interface/implementation khác nhau nhưng gần như giống hệt nhau**, đăng ký DI riêng biệt. FE có gọi cả hai, khớp đúng route, không phải bug — nhưng là nợ kỹ thuật ở BE nên cân nhắc gộp lại.

## 3. Tính năng Backend đã có nhưng Frontend chưa tích hợp UI

| Tính năng Backend | Route | Ghi chú |
|---|---|---|
| Nộp bằng chứng xác minh kỹ năng/chứng chỉ (AI tự chấm + escalate) | `POST/GET /expert/verifications`, `POST /expert/verifications/{id}/escalate` | Tính năng mới nhất theo git log (`1f682e8`) — **FE chưa có màn hình nào cho expert nộp bằng chứng** |
| Danh sách kỹ năng hệ thống + gắn kỹ năng vào profile expert | `GET /skills`, `POST /skills/expert/me`, `DELETE /skills/expert/me/{skillId}` | FE **không gọi `GET /skills` ở đâu cả**; `PostJobPage.tsx:593` gửi cứng `skillIds: []` khi tạo job → job luôn được tạo không gắn skill nào |
| AI viết mô tả dịch vụ cho Expert | `POST /ai/service-generator` | Không có UI |
| Từ chối gợi ý AI job assistant | `POST /ai/job-assistant/{id}/reject` | Không có UI |
| Hủy / xóa job | `POST /jobs/{id}/cancel`, `DELETE /jobs/{id}` | Không có nút hủy/xóa job ở FE |
| Expert rút đề xuất | `PUT /proposals/{id}/withdraw` | Hằng số endpoint đã định nghĩa ở FE (`PROPOSALS.WITHDRAW`) nhưng không nơi nào gọi — không có nút "rút đề xuất" |
| Duyệt yêu cầu cập nhật hồ sơ expert | `GET/PUT /admin/expert-profile-updates` | Xem mục 2.1 |
| Xóa file đã upload | `DELETE /media/{publicId}` | Không có UI xóa file — BE đã bổ sung `GET /media` (list) + cho phép user thường tự xóa media của mình (xem mục 0, PR #87). Không còn gì vướng từ phía BE để làm UI này |
| Mở dispute nhanh từ milestone | `POST /milestones/{id}/dispute` | FE mở dispute qua `POST /disputes` trực tiếp thay vì dùng shortcut này — không phải bug, chỉ là route BE không được dùng |
| Thêm milestone mới vào project đã hired | `POST /projects/{id}/milestones` | FE chỉ đọc/fund/approve milestone có sẵn (tạo lúc accept proposal), không có UI thêm milestone mới sau đó |
| Chuyển tiền ví trực tiếp Client → Expert (ngoài escrow) | `POST /wallet/transfer/{expertId}` | Không có UI, chưa rõ có cần cho nghiệp vụ hiện tại không |
| Realtime cập nhật trạng thái job qua SignalR | `JobStatusUpdated` event, hub `/api/v1/chat` | Xem mục 2.2 — BE đã emit, FE có hook nhưng chưa tự mở connection ngoài màn hình chat |

## 4. Các mảng đã đối chiếu khớp đúng (không cần sửa)

Đã xác minh trực tiếp bằng cách đọc source hai bên (không chỉ dựa vào file OpenAPI cũ):

- **Auth/cookie:** BE set HttpOnly cookie (`accessToken`/`refreshToken`) + trả token trong body; FE dùng `withCredentials`, không tự gắn Bearer header — khớp đúng theo fix gần nhất (`6d50f52`).
- **Wallet:** `POST /wallet/deposit`, `/wallet/vnpay/deposit`, `/wallet/withdraw` — cả 3 đều tồn tại thật ở `WalletController` và khớp shape với FE (file OpenAPI cũ không có 3 route này nên trước đó bị nghi ngờ nhầm là "thiếu").
- **Notifications:** toàn bộ 4 endpoint FE gọi (`GET /notifications`, `/unread-count`, `PUT /{id}/read`, `/read-all`) khớp đúng `NotificationController`.
- **Admin cơ bản:** `GET /admin/stats`, `/admin/users`, `PUT /admin/users/{id}/suspend|unsuspend`, `GET /admin/expert-reviews` (danh sách) — khớp đúng.
- **Profiles/Search:** `GET /profiles/experts/search`, `/profiles/experts/featured` — khớp đúng `ProfileController`.
- **`POST /ai/jobs/{jobId}/refine`** — khớp đúng, không phải route chết như nghi ngờ ban đầu.
- **Disputes** (`close`, `request-evidence`, `DELETE evidence/{id}`) — khớp đúng, chỉ riêng `GET evidence` là sai (đã sửa ở mục 1).
- **Proposals** `unshortlist` — khớp đúng (BE có sẵn `POST /proposals/{id}/unshortlist`).
- **Reviews:** `CreateReviewRequest` ở FE khớp chính xác 100% field với BE (`ProjectId, RevieweeId, Rating, Comment, CommunicationRating, QualityRating, DeadlineRating, RequirementClarityRating`).
- **SignalR ChatHub:** BE map hub tại `/api/v1/chat` (`Program.cs:46`), FE connect tới `${API_URL}/chat` = `.../api/v1/chat` — khớp đúng. Method/event (`SendMessage`, `JoinConversation`, `LeaveConversation`, `UserTyping`, `ReceiveMessage`, `ReadConfirmation`) khớp đúng, chỉ có `"Error"` event là BE không bao giờ emit (lỗi surface qua `HubException` mặc định của SignalR) — không phải bug, chỉ là điểm khác với tài liệu.

## 5. Rủi ro tiềm ẩn (không phải bug hiện tại, nhưng dễ vỡ)

- **Enum:** BE serialize enum thành **string** toàn cục (`JsonStringEnumConverter`), nhưng FE tự map enum sang **số** (`ProjectStatus`, `MilestoneStatus`, `BudgetType`, `SkillLevel`...) song song với fallback đọc string. Hoạt động được nhờ code tolerant ở FE, nhưng nếu BE đổi cách serialize enum bất kỳ enum nào theo kiểu khác (VD: expose enum thay vì string thô cho `PaymentStatus`, `DisputeStatus`, `ExpertVerificationStatus`...) thì các mapping số ở FE sẽ không còn đúng.
- **`normalizeBaseResponse`/`normalizePaginatedResponse` ở FE quá tolerant** (chấp nhận cả camelCase/PascalCase, nhiều shape page-result khác nhau) — dấu hiệu cho thấy contract giữa 2 bên từng không ổn định. Việc này che giấu tốt sự sai lệch nhỏ, nhưng cũng có nghĩa nếu BE trả sai field, lỗi sẽ âm thầm bị nuốt thay vì lộ ra ngay.
- **File `Aivoraapi v1.json`** (OpenAPI spec) ở FE đã lỗi thời, thiếu ~15 route thực tế đang chạy (`/admin/*`, `/notifications/*`, `wallet/deposit`, `wallet/withdraw`, `jobs/my`, `profiles/experts/search|featured`, `ai/jobs/{jobId}/refine`...). Nên regenerate lại file này (Scalar/OpenAPI export) để tránh gây hiểu lầm cho dev mới.
- **`docs/flows/API_BY_FLOW.md`** ở BE cũng lệch với code thật ở vài chỗ: sai field `splitPercentage` (thực tế là `ReleaseAmount`/`RefundAmount`) trong resolve dispute, sai shape response của `recommendations` (doc có wrapper `{jobId, generatedAt, recommendations}`, code trả flat list), thiếu tài liệu cho toàn bộ Expert Verification + Admin endpoints.

## 6. Tính năng chưa làm ở cả hai bên

- **Settings / đổi mật khẩu / 2FA** — FE có comment `TODO: These endpoints do not exist in v1.json yet`, cả 3 hàm là mock cứng không gọi network thật. BE cũng không có `SettingsController` hay endpoint đổi mật khẩu nào. Chưa làm ở cả 2 phía.

## 7. Đề xuất thứ tự ưu tiên cho session sửa tiếp theo

1. **Sửa route Admin Expert Review ở FE (mục 2.1)** — BE đã xong (PR #85), FE đang gọi route 404 thật sự (chỉ chạy nhờ mock) — admin không thể duyệt yêu cầu cập nhật hồ sơ expert trên môi trường thật.
2. **Skill picker khi đăng job** (mục 3) — job hiện tại luôn tạo không gắn skill (`skillIds: []` cứng), ảnh hưởng trực tiếp tới chất lượng recommendation matching.
3. **UI nộp bằng chứng Expert Verification** — tính năng BE mới nhất, hoàn toàn chưa có lối vào từ FE.
4. **Bật realtime JobStatusUpdated ở FE (mục 2.2)** — BE đã emit đúng, chỉ cần FE tự mở SignalR connection ngoài màn hình chat.
4. Các mục còn lại ở bảng mục 3 (cancel/delete job, withdraw proposal, xóa media...) — độ ưu tiên thấp hơn, tùy roadmap.
