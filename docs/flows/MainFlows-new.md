# AITasker Business Flows - Direct Transfer Source of Truth

> Purpose: This document is the new business-flow source of truth.
> It replaces the old escrow/wallet interpretation for project payment.
>
> Core payment rule:
> The platform does not legally hold client funds, freeze funds, refund funds, or release funds to experts.
> The platform may keep simulated demo balances and transaction records for MVP/testing only.
> Those records must be presented as direct-transfer tracking/evidence, not real wallet custody or escrow.

## Compatibility Note

Existing code and schema may still use legacy technical names such as `Wallets`, `Payments`, `WalletTransactions`, `HELD`, and `RELEASED`.

In the new business meaning:

* `Wallets` means simulated demo balance only, not a legal user wallet.
* `Payments` means direct-transfer tracking record for a milestone.
* `WalletTransactions` means simulated transfer event ledger.
* `HELD` means transfer initiated / waiting expert receipt confirmation.
* `RELEASED` means expert confirmed receipt or milestone payment record completed.

These legacy names should not be shown to users as escrow, held client funds, fund freeze, refund, or platform payout.

## Business Flow 1: Create Job & Match Expert

**Actors:** Client, System, Expert

### Main Flow

1. **Client** creates a job.
2. **Client** enters the job requirement. *(Job Post status: `DRAFT`)*
3. **System** generates AI suggestions based on the input.
4. **Client** edits the job content based on the suggestions or personal preference.
5. **Client** publishes the job. *(Job Post status: `OPEN`)*
6. **System** recommends suitable experts.
7. **Expert** views the open job.

### Exceptions / Alternative Flows

* **At Step 2 - Incomplete Requirement:** If the requirement is incomplete, the **System** asks clarifying questions and the **Client** provides additional information.
* **At Step 5 - Validation Failed:** If publishing fails due to validation errors, the **Client** fixes missing information and tries publishing again.
* **At Step 6 - No Match:** If no suitable expert is matched, the job stays `OPEN` and the **Client** may refine the job details.
* **At Step 7 - No Applicants:** If no expert applies yet, the job remains open on the marketplace.

---

## Business Flow 2: Proposal, Agreement Snapshot & Project Creation

**Actors:** Expert, Client, System

### Main Flow

1. **Expert** views the job list and job detail.
2. **Expert** creates and submits a proposal.
3. **Client** reviews the proposals.
4. **Client** accepts one proposal and confirms the first milestone payment amount. *(Job Post status: `IN_PROGRESS`, Proposal status: `ACCEPTED`)*
5. **System** creates the project and a reference agreement snapshot.
6. **Client** may upload a contract document or supporting file. *(The system stores it as evidence only and does not verify legal validity.)*
7. **Expert** may review and acknowledge the uploaded contract/evidence.
8. The project is ready for direct milestone payment.

### Exceptions / Alternative Flows

* **At Step 2 - Invalid Proposal:** If the proposal is invalid or duplicate, the **Expert** edits and resubmits it.
* **At Step 3 - Client Rejection/Clarification:** If the **Client** rejects the proposal, the job remains `OPEN`. If the **Client** shortlists or requests clarification, the **System** updates proposal status.
* **At Step 4 - Payment Amount Negotiation:** If the **Expert** requests an adjustment, the **Client** must review and agree to the new amount before project payment starts.
* **At Step 6 - No Contract Uploaded:** If no contract is uploaded, the **System** proceeds with the agreement snapshot only and shows an evidence limitation warning.
* **At any step between 2-4 - Proposal Updates:** If the **Expert** adjusts the proposal before acceptance, the expert-side flow returns to Step 2.

---

## Business Flow 3: Project Management, Direct Payment, Deliverable, Dispute & Review

**Actors:** Client, System, Expert, Admin

### Main Flow: Payment & Work

1. **Client** selects, creates, or edits a milestone. *(Only the Client can create or edit milestones during the project.)*
2. **System** prepares a direct-transfer payment record for the milestone.
3. **Client** initiates the direct transfer to the **Expert** outside the platform or through a simulated external payment action.
4. **System** records transfer evidence/status only. *(The platform does not hold the money.)*
5. **Expert** confirms receipt of the direct transfer.
6. **Expert** starts working. *(Optional: AI assists or monitors throughout the project process; System sends notifications for major updates.)*
7. **Expert** submits the deliverable.
8. **Client** reviews the deliverable.

### Direct Payment Rules

* The platform may maintain simulated demo balances for MVP/testing only.
* The platform must not describe simulated balances as legally held client funds.
* The platform must not freeze, refund, or release real funds.
* Payment and transaction records are for tracking, evidence, reporting, demo balance, and project state only.
* If platform commission is needed, the system records the commission amount for reporting/invoice purposes only. Commission collection is out of scope for MVP.

### Recommended Status Transitions (Minimum-Code-Change Mapping)

```text
Payment:
PENDING -> HELD -> RELEASED
PENDING / HELD -> FAILED

Payment status meaning:
PENDING = transfer record created
HELD = client initiated simulated direct transfer / waiting expert confirmation
RELEASED = expert confirmed receipt or payment record completed
FAILED = simulated transfer failed
FROZEN / REFUNDED / PARTIALLY_RELEASED = legacy escrow statuses, not used in the new main flow

Milestone:
CREATED -> FUNDED -> IN_PROGRESS -> SUBMITTED -> PAID

Milestone status meaning:
FUNDED = receipt confirmed / ready to work, not escrow funded
PAID = milestone completed after deliverable approval, not platform payout

Revision path:
SUBMITTED -> REVISION_REQUESTED -> SUBMITTED

Dispute path:
FUNDED / IN_PROGRESS / SUBMITTED / REVISION_REQUESTED -> DISPUTED

Project:
PENDING_PAYMENT -> ACTIVE -> IN_REVIEW -> COMPLETED
ACTIVE / IN_REVIEW -> DISPUTED
```

### Split: Deliverable & Resolution

* **Branch 8A - Approve:**
    * **Client** approves the deliverable.
    * Milestone is completed.
    * If all milestones are completed, the project finishes.
    * Otherwise, the flow loops back to Step 1 for the next milestone.
    * **Client** may review the **Expert** after project completion.
    * **Expert** may review the **Client** after project completion.

* **Branch 8B - Request Revision:**
    * **Client** requests a revision.
    * **Expert** revises the work.
    * **Expert** resubmits the deliverable.
    * Flow loops back to Step 8.

* **Branch Exception - Dispute:**
    * **Client** or **Expert** flags the milestone.
    * **System** updates the milestone/project dispute state to show `DISPUTED`.
    * The platform does not freeze or reverse money.
    * Further settlement happens outside the platform or by mutual cancellation.

### Exceptions / Alternative Flows

* **At Step 3 - Direct Transfer Failed:** The transfer fails or cannot be verified, so the **Client** must retry or update transfer evidence.
* **At Step 5 - Receipt Not Confirmed:** If the **Expert** does not confirm receipt, the milestone stays payment-pending and work should not start in the platform flow.
* **At Step 7 - Late/Missing Deliverable:** If the deliverable is late or missing, the **Client** may remind the **Expert** via Chat or modify future milestones.
* **At any step - Dispute Available:** If a dispute is opened, the flow turns into the dispute branch.
* **Review Timeout:** At the end of the project, if no review is submitted, the project remains completed without a review.

---

## Implementation Boundary

The new source of truth keeps the existing transaction structure where possible, but changes the business meaning:

* Client demo balance updates may exist for simulation only, not real custody.
* `Wallets` may remain as a technical table, but it means simulated demo balance.
* `WalletTransactions` may remain as the simulated transfer/event ledger.
* `Payments` may remain as the milestone direct-transfer tracking record.
* Payment status `HELD` may remain as a technical value, but it means transfer initiated / waiting receipt confirmation.
* Payment status `RELEASED` may remain as a technical value, but it must not be described as platform payout.
* Payment statuses `FROZEN`, `REFUNDED`, and `PARTIALLY_RELEASED` are legacy escrow statuses and are not used in the new main flow.
* Dispute should flag project/milestone state and evidence; it should not imply the platform freezes or reverses money.

Existing code or older documents may still contain escrow wording. Treat that wording as legacy and update UI/API/docs labels toward direct-transfer tracking first, before larger code refactors.
