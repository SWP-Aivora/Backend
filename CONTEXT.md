# CONTEXT — Aivora Domain Model

## 📚 Bounded Context: Aivora Platform

**Mục đích:** Kết nối Client với Expert thông qua marketplace cho các dự án AI/tech với escrow payment, dispute resolution, và review system.

### 🎯 Thuật ngữ chính (Ubiquitous Language)

#### 🔑 Core Domain Objects

**User**
- *Bản chất:* Thực thể pháp lý nền tảng
- *Thuộc tính:* Email, PasswordHash, FullName, AvatarUrl, Phone, Role (CLIENT/EXPERT/ADMIN), Status (ACTIVE/SUSPENDED/DELETED)
- *Quan trọng:* Mọi thành phần hệ thống đều là User ở tầng cơ sở

**Client**
- *Bản chất:* Người thuê dịch vụ (mặt người dùng cuối)
- *Quan hệ:* Mỗi Client có exactly một User (1:1)
- *Thuộc tính:* CompanyName, Industry, CompanySize, Website, Description, Rating, TotalReviews
- *Ranh giới:* Không thể tồn tại độc lập - phải gắn với User

**Expert**
- *Bản chất:* Người cung cấp dịch vụ chuyên môn
- *Quan hệ:* Mỗi Expert có exactly một User (1:1)
- *Thuộc tính:* Title, Bio, HourlyRate, ExperienceYears, AvailabilityStatus, Rating, TotalReviews, CompletedProjects, SuccessRate, ResponseTimeMinutes
- *Ranh giới:* Chuyên môn được thể hiện qua ExpertSkills - không thể là Expert mà không có kỹ năng

**Skill**
- *Bản chất:* Năng lực chuyên môn có thể đánh giá
- *Thuộc tính:* Name (ví dụ: "Python", "Machine Learning"), Category
- *Quan hệ:* 
  - One-to-many với JobPost thông qua JobSkill
  - One-to-many với ExpertProfile thông qua ExpertSkill
- *Chú ý:* Skill có thể được sử dụng bởi cả Client (tìm kiếm) và Expert (thể hiện năng lực)

**JobPost**
- *Bản chất:* Nhu cầu dự án từ Client
- *Thuộc tính:* Title, Description, Budget, Currency, Scope, Timeline, Skills (thông qua JobSkill)
- *Luồng sống:* 
  - DRAFT → PUBLISHED → CLOSED (hoặc EXPIRED, CANCELLED)
  - Khi Client accept proposal → Project được tạo
- *Quan hệ:* Many-to-one với Client, one-to-many với Milestone, one-to-many với Proposal

**Proposal**
- *Bản chất:* Đề xuất giá cả và phương án từ Expert
- *Thuộc tính:* QuoteAmount, TimelineDays, ProposalText, Status (DRAFT/SUBMITTED/ACCEPTED/REJECTED/CANCELLED)
- *Luồng sống:* 
  - SUBMITTED → ACCEPTED/REJECTED/CANCELLED
  - ACCEPTED sẽ tạo Project
- *Ranh giới:* Một JobPost có thể có nhiều Proposal, nhưng chỉ có một được ACCEPTED

**Project**
- *Bản chất:* Dự án đang diễn ra sau khi proposal được chấp nhận
- *Thuộc tính:* Title, Description, TotalBudget, Currency, Status (PENDING_PAYMENT/IN_PROGRESS/COMPLETED/CANCELLED), StartDate, EndDate
- *Quan hệ:* 
  - One-to-one với JobPost và AcceptedProposal
  - One-to-many với Milestone
  - Many-to-one với Client và Expert
- *Quan trọng:* Project là sự thể hiện cụ thể của JobPost đã được chấp nhận

**Milestone**
- *Bản chất:* Giai đoạn thanh toán có điều kiện
- *Thuộc tính:* Title, Description, Amount, DueDate, Status (PENDING/APPROVED/REJECTED/PAID), CompletedAt
- *Luồng sống:* PENDING → APPROVED → PAID
- *Quan hệ:* Many-to-one với Project, one-to-many với Deliverable
- *Ranh giới:* Mỗi Project có ít nhất một Milestone

**Deliverable**
- *Bản chất:* Công việc cụ thể trong một milestone
- *Thuộc tính:* Title, Description, Status (TODO/IN_PROGRESS/DONE), FileUrl (nếu có)
- *Quan hệ:* Many-to-one với Milestone
- *Chú ý:* Đây là cấp độ chi tiết nhất của công việc

#### 💰 Payment Domain

**Wallet**
- *Bản chất:* Tài khoản escrow cho từng User
- *Thuộc tính:* Balance, Currency
- *Luồng sống:* Balance có thể tăng (deposit) hoặc giảm (withdrawal)
- *Quan hệ:* One-to-one với User
- *Quan trọng:* Tất cả transaction đều đi qua Wallet

**Payment**
- *Bản chất:* Bản ghi giao dịch tài chính
- *Thuộc tính:* Amount, Currency, Type (DEPOSIT/WITHDRAWAL/ESCROW_RELEASE), Status (PENDING/COMPLETED/FAILED), ReferenceId
- *Quan hệ:* Many-to-one với User, Many-to-one với Wallet
- *Luồng sống:* PENDING → COMPLETED/FAILED
- *Ranh giới:* Payment là kết quả của các hoạt động tài chính thực tế

**WalletTransaction**
- *Bản chất:* Bản ghi chi tiết mọi thay đổi wallet
- *Thuộc tính:* Amount, Type (DEPOSIT/WITHDRAWAL/ESCROW_HOLD/ESCROW_RELEASE), Description, BalanceAfter
- *Quan hệ:* Many-to-one với Wallet
- *Chú ý:* Dùng cho tracking lịch sử thay đổi balance

#### 💬 Communication Domain

**Conversation**
- *Bản chất:* Chat giữa Client và Expert cho một Project
- *Thuộc tính:* CreatedAt, LastMessageAt
- *Quan hệ:* One-to-many với Message, Many-to-one với Project
- *Ranh giới:* Conversation chỉ tồn tại khi Project đã được tạo

**Message**
- *Bản chất:* Tin nhắn riêng tư trong conversation
- *Thuộc tính:* Content, MessageType (TEXT/FILE), SentAt, ReadAt
- *Quan hệ:* Many-to-one với Conversation, Many-to-one với User
- *Luồng sống:* SENT → READ (optional)

#### ⚖️ Quality & Trust Domain

**Review**
- *Bản chất:* Đánh giá sau khi Project hoàn thành
- *Thuộc tính:* Rating (1-5), Comment, ReviewType (CLIENT_TO_EXPERT/EXPERT_TO_CLIENT)
- *Luồng sống:* Draft → SUBMITTED
- *Quan hệ:* Many-to-one với Project, Many-to-one với User
- *Quan trọng:* 
  - Chỉ review sau khi Project là COMPLETED
  - Self-review bị cấm
  - Một cặp user-project chỉ có thể review một lần mỗi hướng

**Dispute**
- *Bản chất:* Xung đột giữa Client và Expert
- *Thuộc tính:* Reason, ResolutionStatus (OPEN/IN_REVIEW/RESOLVED), CreatedAt, ResolvedAt
- *Quan hệ:* Many-to-one với Project
- *Ranh giới:* Chỉ mở khi Project đang diễn ra

**DisputeEvidence**
- *Bản chất:* Bằng chứng cho dispute
- *Thuộc tính:* FileUrl, Description
- *Quan hệ:* Many-to-one với Dispute

#### 🤖 AI Assistant Domain

**AIJobSuggestion**
- *Bản chất:* Gợi ý Job từ hệ thống AI
- *Thuộc tính:* Title, Description, SuggestedSkills, ConfidenceScore
- *Quan hệ:* Many-to-one với User

**RecommendationResult**
- *Bản chất:* Kết quả recommendation từ AI
- *Thuộc tính:* Type (EXPERT_MATCH/JOB_SUGGESTION), Payload (JSON), Score
- *Quan hệ:* Many-to-one với User

#### 📢 Notification Domain

**Notification**
- *Bản chất:* Thông báo cho người dùng
- *Thuộc tính:* Title, Message, Type (INFO/WARNING/ERROR), ReadAt
- *Quan hệ:* Many-to-one với User
- *Luồng sống:* UNREAD → READ

### 🔄 Business Rules

1. **User Creation Flow**
   - Tạo User trước → mới có thể tạo Client/Expert Profile
   - Role là CLIENT/EXPERT/ADMIN - không thể thay đổi sau khi tạo

2. **Job Posting Flow**
   - Client phải có profile hoàn chỉnh để post job
   - Job status: DRAFT → PUBLISHED → CLOSED (hoặc EXPIRED, CANCELLED)
   - Khi job được accept → tự động tạo Project

3. **Project Lifecycle**
   - PENDING_PAYMENT: Chờ deposit vào escrow
   - IN_PROGRESS: Dự án đang diễn ra
   - COMPLETED: Dự án hoàn thành (có thể review)
   - CANCELLED: Dự án bị hủy

4. **Milestone Payment Flow**
   - Client deposit vào wallet trước
   - Khi milestone APPROVED → tiền được transfer từ wallet sang expert
   - Self-approval bị cấm

5. **Review Rules**
   - Chỉ review sau khi Project = COMPLETED
   - Rating 1-5, không thể thay đổi
   - Mỗi user-project chỉ có thể review một lần mỗi hướng

6. **Dispute Resolution**
   - Chỉ mở khi Project đang diễn ra (IN_PROGRESS)
   - Admin hoặc hệ thống tự động xử lý

### 🏗️ Key Aggregates

1. **User Aggregate** [User]
   - Root: User
   - Children: ClientProfile, ExpertProfile, Wallet
   - Boundary: Authentication và profile management

2. **JobPost Aggregate** [JobPost]
   - Root: JobPost
   - Children: JobSkill, Proposal
   - Boundary: Job posting và bidding process

3. **Project Aggregate** [Project]
   - Root: Project
   - Children: Milestone, Deliverable, Conversation
   - Boundary: Dự án đang diễn ra

4. **Payment Aggregate** [Wallet]
   - Root: Wallet
   - Children: Payment, WalletTransaction
   - Boundary: Tài chính và escrow management

### 🎨 Domain Events

- `JobPosted`: Job được publish
- `ProposalSubmitted`: Expert submit proposal
- `ProjectCreated`: Proposal được accept
- `MilestoneApproved`: Client approve milestone
- `PaymentReleased`: Tiền được transfer sang expert
- `ProjectCompleted`: Dự án hoàn thành
- `ReviewSubmitted`: Client/Expert submit review
- `DisputeRaised`: Xung đột được báo cáo