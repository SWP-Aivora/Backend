# Direct-Transfer Source Alignment Implementation Plan

  > Tracking rule: Use checkbox - [ ] and mark each task complete
  > only after code/docs/tests and phase review pass.
  > Pre-code rule: Before editing any C# file, read and follow C:
  > \Users\NITRO\.claude\rules\ecc\csharp\coding-style.md,
  > patterns.md, security.md, testing.md, and hooks.md.

  Goal: Update source code, tests, and docs so the backend fits
  docs/flows/MainFlows-new.md using compatibility direct-transfer
  semantics.

  Architecture: Keep current controllers, DTOs, enums, services,
  and schema names where possible. Change user-facing meaning from
  escrow/custody/freeze/payout to simulated direct-transfer
  tracking and add verification for documented API request/response
  truth.

  Tech Stack: .NET 10, ASP.NET Core, EF Core, xUnit,
  FluentAssertions, Moq, EF Core InMemory.

  ———

  ## Phase 0: Execution Preflight

  ### Task 0.1: Confirm working branch and rules

  Files will change: None.

  Goal: Prevent accidental edits on wrong branch and enforce C#
  clean-code rules.

  Acceptance criteria:

  - [ ] git status --short --branch shows branch docs/code-status-
    reconciliation.

  - [ ] Read all rule files under C:
    \Users\NITRO\.claude\rules\ecc\csharp.

  - [ ] Record in implementation notes that C# changes follow
    explicit guard clauses, safe client errors, xUnit/
    FluentAssertions, no secrets in config.

  Exceptions:

  - [ ] If branch is different, stop and switch only after
    confirming with user.

  ### Phase 0 Review

  - [ ] Branch, baseline, and rule files confirmed before any code
    edit.

  ———

  ## Phase 1: Stabilize Documentation Source Of Truth

  ### Task 1.1: Resolve API_BY_FLOW.md conflict

  Files will change:

  - docs/flows/API_BY_FLOW.md

  Goal: Make the API flow document readable and authoritative.

  Acceptance criteria:

  - [ ] Remove <<<<<<< HEAD, =======, >>>>>>> main.
  - [ ] Keep the concise compatibility direct-transfer version.
  - [ ] Delete duplicated legacy escrow section.
  - [ ] Keep docs/flows/MainFlows-new.md as source of truth.
  - [ ] rg -n "<<<<<<<|=======|>>>>>>>" docs returns no results.

  Exceptions:

  - [ ] Do not edit docs/flows/legacy/MAINFLOW_v1.md.

  ### Task 1.2: Fix README and architecture drift

  Files will change:

  - README.md
  - CLAUDE.md
  - docs/ARCHITECTURE.md
  - docs/flows/README.md

  Goal: Stop top-level docs from advertising escrow as current
  behavior.

  Acceptance criteria:

  - [ ] Replace active escrow wording with simulated direct-
    transfer tracking.

  - [ ] Fix missing docs/flows/MAINFLOW.md links to docs/flows/
    MainFlows-new.md.

  - [ ] Architecture removes stale claims not present in code,
    including FinancialLedgerEntry, generic IRepository<T>, and
    ServiceBase if still absent.

  - [ ] Legacy technical names are documented as compatibility
    names, not user-facing business meaning.

  Exceptions:

  - [ ] Do not rename tables, enums, routes, or services in this
    phase.

  ### Phase 1 Review

  - [ ] Re-read changed docs and verify no current-source
    contradiction remains.

  - [ ] Run rg -n "MAINFLOW.md|escrow payment|fund escrow"
    README.md CLAUDE.md docs -S; only legacy/reference mentions
    allowed.

  ———

  ## Phase 2: API Request/Response Truth Audit

  ### Task 2.1: Build endpoint truth table

  Files will change:

  - docs/flows/API_BY_FLOW.md

  Source files to inspect:

  - Aivora.api/Controllers/*.cs
  - Aivora.Services/*Service/Request.cs
  - Aivora.Services/*Service/Response.cs
  - Aivora.Repositories/Entities/*.cs
  - Aivora.Repositories/Enums/*.cs

  Goal: Check every documented endpoint request/response as TRUE,
  FALSE, or PARTIAL.

  Acceptance criteria:

  - [ ] Add an “API Request/Response Verification” table to
    API_BY_FLOW.md.

  - [ ] For every endpoint in the doc, verify route, HTTP method,
    auth policy, request body, response fields, status transitions,
    and side effects.

  - [ ] Mark each row TRUE, FALSE, or PARTIAL.
  - [ ] For every FALSE or PARTIAL, add exact mismatch and expected
    fix.

  - [ ] Specifically check AI job assistant, jobs, proposals,
    projects, milestones, wallet/payments, deliverables, disputes,
    reviews, auth/profile, skills/categories, messages, media,
    notifications, admin.

  Exceptions:

  - [ ] Do not change code during this audit task.
  - [ ] Do not trust examples until matched against DTO/service
    source.

  ### Task 2.2: Fix documentation examples after truth audit

  Files will change:

  - docs/flows/API_BY_FLOW.md

  Goal: Make request/response examples match current or planned
  compatibility behavior.

  Acceptance criteria:

  - [ ] Correct false request field names and response field names.
  - [ ] Remove impossible response examples.
  - [ ] Mark planned code fixes clearly when source will be changed
    in later phases.

  - [ ] Preserve direct-transfer compatibility language.

  Exceptions:

  - [ ] Do not document deep refactor routes that do not exist.

  ### Phase 2 Review

  - [ ] Re-run route scan: rg -n "\[(Route|Http(Get|Post|Put|Patch|
    Delete))" Aivora.api\Controllers.

  - [ ] Re-read API_BY_FLOW.md and verify every FALSE/PARTIAL has a
    linked task in later phases or is explicitly docs-only fixed.

  ———

  ## Phase 3: Align Main Direct-Transfer Semantics

  ### Task 3.1: Update controller messages and DTO business meaning

  Files will change:

  - Aivora.api/Controllers/MilestoneController.cs
  - Aivora.api/Controllers/WalletController.cs
  - Aivora.api/Controllers/PaymentController.cs
  - Aivora.Services/MilestoneService/Response.cs
  - Aivora.Services/WalletService/Response.cs
  - Aivora.Services/WalletService/Service.cs

  Goal: Stop API responses from presenting compatibility records as
  escrow/custody/payout.

  Acceptance criteria:

  - [ ] /milestones/{id}/fund message says direct transfer
    recorded.

  - [ ] /milestones/{id}/approve message says payment record
    completed.

  - [ ] Wallet response exposes simulated demo-balance meaning.
  - [ ] Transaction/payment response exposes legacy status business
    meaning.

  - [ ] Existing technical fields like heldBalance, HELD, RELEASED
    may remain.

  Exceptions:

  - [ ] No new receipt-confirmation endpoint.
  - [ ] No migration.

  ### Task 3.2: Reword Treasury and service semantics

  Files will change:

  - Aivora.Services/Treasury/ITreasury.cs
  - Aivora.Services/Treasury/Treasury.cs
  - Aivora.Services/MilestoneService/Service.cs

  Goal: Keep method names stable but remove active escrow/fund-
  freeze language from comments, logs, and validation messages.

  Acceptance criteria:

  - [ ] User-facing errors no longer say “held funds”, “escrow”,
    “release funds”, or “frozen funds”.

  - [ ] Logs use structured properties and direct-transfer/demo
    wording.

  - [ ] Simulated balance movements stay unchanged for
    compatibility.

  - [ ] PaymentStatus.HELD and PaymentStatus.RELEASED remain
    technical values.

  Exceptions:

  - [ ] Do not rename FundMilestoneAsync or ReleaseMilestoneAsync
    in this pass.

  ### Phase 3 Review

  - [ ] Run focused tests for milestone/wallet/payment services.
  - [ ] Search changed source for forbidden user-facing wording.
  - [ ] Confirm no secrets or appsettings real values changed.

  ———

  ## Phase 4: Fix Dispute Flow

  ### Task 4.1: Stop normal dispute from freezing payment records

  Files will change:

  - Aivora.Services/DisputeService/Service.cs
  - Aivora.Services/Treasury/Treasury.cs
  - Aivora.Tests/Services/DisputeServiceTests.cs
  - Aivora.Tests/Services/E2EBusinessFlowTests.cs

  Goal: A dispute flags project/milestone state but does not imply
  platform freezes or reverses money.

  Acceptance criteria:

  - [ ] OpenDisputeAsync sets dispute OPEN, milestone DISPUTED,
    project DISPUTED.

  - [ ] OpenDisputeAsync does not call FreezeFundsAsync.
  - [ ] Payment remains HELD as legacy tracking record in normal
    client/expert dispute path.

  - [ ] Tests assert payment is not changed to FROZEN.

  Exceptions:

  - [ ] Keep legacy/admin resolution endpoint available.

  ### Task 4.2: Clarify admin resolution as legacy/demo-only

  Files will change:

  - Aivora.api/Controllers/DisputeController.cs
  - Aivora.Services/DisputeService/Request.cs
  - Aivora.Services/DisputeService/Service.cs
  - Aivora.Services/Treasury/Treasury.cs
  - docs/flows/API_BY_FLOW.md

  Goal: Keep admin dispute resolution deterministic without making
  it normal payment custody behavior.

  Acceptance criteria:

  - [ ] Docs mark /disputes/{id}/resolve as admin/demo-only.
  - [ ] Validation messages describe demo records, not real refunds
    or controlled funds.

  - [ ] Client/expert main flow does not require admin money
    resolution.

  - [ ] Tests cover admin refund/split/release behavior that
    remains.

  Exceptions:

  - [ ] Do not remove public API route.

  ### Phase 4 Review

  - [ ] Re-read dispute docs and tests together.
  - [ ] Run dotnet test Aivora.sln -c Release --no-build --filter
    "Dispute|E2E" after build.

  ———

  ## Phase 5: DTO, Validation, And Reporting Gaps

  ### Task 5.1: Preserve deliverable evidence fields

  Files will change:

  - Aivora.Services/DeliverableService/Service.cs
  - Aivora.Tests/Services/E2EBusinessFlowTests.cs

  Goal: Make deliverable response match documented request/
  response.

  Acceptance criteria:

  - [ ] FileUrl, DemoUrl, SourceCodeUrl, and Note map from entity
    to response.

  - [ ] Tests assert evidence fields round-trip.
  - [ ] No schema change, because entity already has fields.

  Exceptions:

  - [ ] Do not create migration.

  ### Task 5.2: Add documented negative validation

  Files will change:

  - Aivora.Services/DeliverableService/Service.cs
  - Aivora.Services/ReviewService/Service.cs
  - Aivora.Tests/Services/E2EBusinessFlowTests.cs
  - Aivora.Tests/Services/MilestoneServiceTests.cs

  Goal: Turn important negative cases from docs into executable
  tests.

  Acceptance criteria:

  - [ ] Deliverable submission fails when all evidence fields are
    blank.

  - [ ] Review rating outside 1..5 fails.
  - [ ] Approving before deliverable submission still fails.
  - [ ] Requesting revision leaves payment status unchanged.
  - [ ] Duplicate review still fails.

  Exceptions:

  - [ ] Do not add FluentValidation; use current guard-clause
    style.

  ### Task 5.3: Remove escrow terminology from admin stats

  Files will change:

  - Aivora.Services/AdminService/IAdminService.cs
  - Aivora.Services/AdminService/AdminService.cs
  - Aivora.Tests/Services/AdminServiceTests.cs

  Goal: Prevent admin dashboard API from exposing active escrow
  wording.

  Acceptance criteria:

  - [ ] Replace TotalEscrowAmount with
    TotalSimulatedTransferAmount.

  - [ ] Value still sums Wallet.HeldBalance as legacy simulated
    tracking amount.

  - [ ] Tests assert property and value.

  Exceptions:

  - [ ] Treat this as backend API cleanup; frontend update is out
    of scope unless requested.

  ### Phase 5 Review

  - [ ] Run focused tests for deliverable, review, milestone,
    admin.

  - [ ] Re-run API truth table rows touched by this phase and mark
    fixed.

  ———

  ## Phase 6: Detailed Verification And Release Gate

  ### Task 6.1: Formatting and build gates

  Files will change: None.

  Goal: Match real CI.

  Acceptance criteria:

  - [ ] dotnet restore Aivora.sln succeeds.
  - [ ] dotnet format Aivora.sln --verify-no-changes --verbosity
    diag succeeds.

  - [ ] dotnet build Aivora.sln --no-restore -c Release succeeds.
  - [ ] No new warnings related to changed files.

  Exceptions:

  - [ ] Existing unrelated warnings may remain only if documented.

  ### Task 6.2: Full test suite

  Files will change: None.

  Goal: Prove no regression.

  Acceptance criteria:

  - [ ] dotnet test Aivora.sln --no-build -c Release --verbosity
    normal passes.

  - [ ] Expected minimum: current baseline 63/63 plus any new tests
    added.

  - [ ] If test count decreases, investigate before completion.

  Exceptions:

  - [ ] Do not skip failing tests unless user approves.

  ### Task 6.3: Focused behavioral test runs

  Files will change: None.

  Goal: Verify each changed business area independently.

  Acceptance criteria:

  - [ ] Run milestone/payment tests.
  - [ ] Run dispute tests.
  - [ ] Run deliverable/review tests.
  - [ ] Run E2E business flow tests.
  - [ ] Each focused run passes and has no false assumptions from
    old escrow behavior.

  Recommended commands:

  - [ ] dotnet test Aivora.sln -c Release --no-build --filter
    "Milestone"

  - [ ] dotnet test Aivora.sln -c Release --no-build --filter
    "Dispute"

  - [ ] dotnet test Aivora.sln -c Release --no-build --filter
    "Deliverable|Review"

  - [ ] dotnet test Aivora.sln -c Release --no-build --filter "E2E"

  ### Task 6.4: Documentation and source wording scan

  Files will change: None unless scan finds missed wording.

  Goal: Confirm source and docs no longer contradict direct-
  transfer model.

  Acceptance criteria:

  - [ ] rg -n "escrow|custody|fund freeze|platform payout|release
    funds|held funds|frozen funds" README.md CLAUDE.md docs
    Aivora.api Aivora.Services Aivora.Tests -S returns only allowed
    legacy/compatibility mentions.

  - [ ] Allowed mentions are limited to legacy docs, enum
    technical-name mapping, or explicit compatibility notes.

  - [ ] rg -n "<<<<<<<|=======|>>>>>>>" . returns no conflict
    markers in tracked source/docs.

  ### Task 6.5: API documentation truth re-check

  Files will change:

  - docs/flows/API_BY_FLOW.md only if final audit finds mismatch.

  Goal: Re-check request/response truth after code changes.

  Acceptance criteria:

  - [ ] Every endpoint row in API truth table is TRUE or explicitly
    marked LEGACY/OUT_OF_SCOPE.

  - [ ] No FALSE row remains without a linked follow-up.
  - [ ] Examples match actual DTO property names and enum string
    values.

  ### Task 6.6: Git closeout review

  Files will change: None.

  Goal: Make final diff reviewable.

  Acceptance criteria:

  - [ ] git status --short --branch shows only intentional files.
  - [ ] git diff --check passes.
  - [ ] git diff --stat reviewed.
  - [ ] No appsettings*.json real secrets introduced.
  - [ ] Final summary lists completed phases and any remaining out-
    of-scope items.

  ### Phase 6 Review

  - [ ] All CI-equivalent commands passed.
  - [ ] Focused tests passed.
  - [ ] API request/response verification table is current.
  - [ ] Final diff reviewed for scope, wording, and security.

  ## Assumptions

  - [ ] Alignment depth remains compatibility, not deep route/
    model/schema rename.

  - [ ] docs/flows/API_BY_FLOW.md keeps concise direct-transfer
    section, not duplicated legacy escrow detail.

  - [ ] No database migration is planned.
  - [ ] Frontend/client update is out of scope.
  - [ ] Branch remains docs/code-status-reconciliation.