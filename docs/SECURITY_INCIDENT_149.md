# Security Incident — Issue #149: Secret lộ trong git history

**Trạng thái:** Chờ Boss thực hiện các bước rotate thủ công bên dưới.
**Repo:** `SWP-Aivora/Backend` — **PUBLIC**.

## Những gì thực sự lộ (đã xác nhận qua `git log -S`)

Điều tra cho thấy tiền đề gốc của issue #149 (secret trong `appsettings.Development.json`) **không chính xác** — file đó chưa từng được commit với giá trị thật (đã gitignore từ đầu). Rò rỉ thật nằm ở chỗ khác:

| Secret | Giá trị lộ | Commit | Nằm ở đâu |
|---|---|---|---|
| **Render Postgres connection string** | Host `dpg-d8de…` (region singapore-postgres.render.com), DB `aivora`, user `aivora_user`, password bắt đầu `2VZ2…` (12 ký tự) | các commit đầu tiên, gỡ khỏi tracking tại `d6f8e47` | `Aivora.api/appsettings.Development.json` (lịch sử) |
| **JWT signing secret** | Bắt đầu `Aivora_Super_Secret_Key_2026…` (chuỗi cố định, có "2026" trong tên) | `5764c87`, `fb9fdcc` | `Aivora.api/appsettings.Development.json` (lịch sử) |

Giá trị đầy đủ **không in lại ở đây** — repo này PUBLIC, không thêm chỗ mới lộ secret sống. Boss lấy giá trị đầy đủ bằng `git show d6f8e47^:Aivora.api/appsettings.Development.json` (chạy local, không paste ra nơi công khai) nếu cần đối chiếu khi rotate. (Ghi chú: đã thử kết nối bằng giá trị lộ trong lúc điều tra — server đóng kết nối ngay sau startup packet, khả năng cao đã chết/hết hạn do Render free-tier tự xóa DB sau 30 ngày; không coi đây là bằng chứng đã an toàn, vẫn rotate theo đúng quy trình dưới.)

**Không lộ qua git** (chỉ tồn tại trong file local hiện tại, đã gitignore, chưa từng commit): Cloudinary `ApiKey`/`ApiSecret`, `AIProvider.ApiKey`. → **Không cần rotate các key này** vì lý do #149.

## Quyết định: giữ history, chỉ rotate giá trị

Repo đã PUBLIC nên rewrite history (BFG/`git filter-repo` + force-push) không xóa được rủi ro đã xảy ra (ai đã clone/index rồi thì vẫn có) và tốn công toàn team re-clone. Quyết định: **giữ nguyên git history, chỉ rotate secret thật tại nguồn.**

## Việc agent đã làm

- Connection string lộ trong git history đã **chết** khi thử kết nối (server đóng ngay sau startup packet — khả năng do Render free-tier tự xóa DB sau 30 ngày). Boss cung cấp connection string hiện hành trực tiếp (không qua git) để agent kết nối live DB.
- Đã purge **6 job xác nhận là rác** khớp đúng mô tả issue (title + description literal "desc"): Resubmit test job, Update test job, Fix verify job, Debug job, Debug job2, Final verify job — Boss xác nhận từng job trước khi xóa (transaction, kiểm count trước/sau).
- **Không đụng:** 7 job "Verify.../Fix Verify Job..." tạo ngày 19/7 (sau khi issue #149 mở, có thể đang phục vụ verify cho issue khác — Boss chọn giữ lại); các job của tài khoản `dnqa@gmail.com` (nội dung thật, không phải rác dù tên client trùng pattern trong issue — Boss chọn giữ nguyên).
- **Không đụng** 2 admin yếu/joke đang ACTIVE trên live DB (`admin@aivora.com` pass `123456`, `ahihi@aivora.com` pass `ahihi123`) — Boss chọn bỏ qua phần này trong lượt xử lý này; seed code (mục dưới) chỉ ngăn tái diễn cho DB mới, không tự sửa 2 tài khoản đã tồn tại. Cần Boss tự xử lý riêng nếu muốn.

## Việc Boss cần làm (thao tác dashboard, agent không có quyền)

### 1. Rotate password Render Postgres
1. Render Dashboard → database `aivora-db` → đổi mật khẩu (hoặc reset credentials).
2. Cập nhật env var `ConnectionStrings__DefaultConnection` trên service `aivora-backend` (đã đặt `sync: false` trong `render.yaml` — sửa tay trên dashboard) với connection string mới.
3. Deploy lại service.
4. **Lưu ý:** connection string trong history dùng user `aivora_user`, khác với `user: postgres` khai báo trong `render.yaml` cho `databases:` — Render tự sinh username thật lúc provision, giá trị khai báo trong yaml chỉ là gợi ý. Không giả định 2 giá trị này khớp; lấy connection string thật từ dashboard.

### 2. Rotate JWT secret
1. Sinh secret mới (≥32 ký tự ngẫu nhiên).
2. Cập nhật env `JwtSettings__Secret` trên Render dashboard (`sync: false`).
3. Deploy lại.

**Tác động thực tế khi rotate JWT secret (đã kiểm code, nhẹ hơn suy đoán ban đầu):**
- Access token đang lưu ở client (ký bằng secret cũ) sẽ **invalid ngay** → request tiếp theo trả 401.
- **Refresh token KHÔNG bị ảnh hưởng** — nó là chuỗi random, lưu hash SHA-256 trong DB (`IdentityService/Service.cs:118-119`), **không** liên quan `JwtSettings:Secret`. Endpoint refresh chỉ so hash trong DB, không verify chữ ký JWT cũ.
- Kết quả: client có refresh token hợp lệ (còn hạn 7 ngày) sẽ **tự động lấy access token mới** ở lần gọi refresh kế tiếp — **không bắt buộc user đăng nhập lại**, miễn frontend có xử lý 401 → gọi `/refresh` (kiểm tra hành vi thực tế của FE nếu muốn chắc chắn).
- Access token hiện có hạn `JwtSettings__ExpiryInMinutes = 1440` (24h) theo `render.yaml` — vòng đời ngắn, rủi ro token cũ bị lộ bị lạm dụng cũng giới hạn theo đó.

**Không rõ giá trị secret Production hiện tại có trùng với secret lộ trong history hay không** (`JwtSettings__Secret` đặt `sync: false`, agent không đọc được giá trị đang chạy trên Render). Nếu Production đã dùng secret khác từ đầu (được set tay, không lấy từ file lộ), việc rotate ở đây chỉ là vệ sinh phòng ngừa, không phải khắc phục một khai thác đang diễn ra. Nếu trùng, đây là khắc phục thật.

### 3. Không cần hành động (đối chiếu)
- Cloudinary key/secret, AIProvider key: chưa từng lộ qua git — bỏ qua rotate cho #149 (rotate riêng nếu có lý do khác).

## Ghi chú seed credential (đã xử lý trong code, xem Implementation Plan #149)

- Xóa admin joke `ahihi@aivora.com` khỏi seeder.
- Password seed (`admin@aivora.com`, client*, expert*) giờ đọc từ config `Seed:DefaultPassword` (env `Seed__DefaultPassword`), không còn hardcode `123456`.
- Demo-data seed (bao gồm 2 account admin) **không chạy trên môi trường Production** (`Program.cs` guard `!app.Environment.IsProduction()`) — chỉ ngăn tái diễn cho DB mới.
- **Chưa xử lý:** 2 account yếu đã tồn tại sẵn trên live DB (`admin@aivora.com`, `ahihi@aivora.com`) — xem mục "Việc agent đã làm" ở trên, Boss chủ động chọn bỏ qua trong lượt này.
