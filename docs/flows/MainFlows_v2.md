# Business Flows

## Business Flow 1: Create Job & Match Expert
**Actors:** Client, System, Expert

### Main Flow:
1. **Client** creates a job.
2. **Client** enters the job requirement. *(Job Post status: "DRAFT")*
3. **System** generates AI suggestions based on the input.
4. **Client** edits the job content based on the suggestions or personal preference.
5. **Client** publishes the job. *(Job Post status: "OPEN")*
6. **System** recommends suitable experts.
7. **Expert** views the open job.

### Exceptions / Alternative Flows:
* **At Step 2 (Incomplete Requirement):** If the requirement is incomplete -> The **System** asks clarifying questions -> The **Client** provides additional information.
* **At Step 5 (Validation Failed):** If publishing fails due to validation errors -> The **Client** fixes missing information -> Tries publishing again.
* **At Step 6 (No Match):** If no suitable expert is matched -> The job stays OPEN -> The **Client** may refine the job details (loops back to Step 4).
* **At Step 7 (No Applicants):** If no expert applies yet -> The job remains open on the marketplace.
---

## Business Flow 2: Proposal, Agreement Snapshot & Project Creation
**Actors:** Expert, Client, System

### Main Flow:
1. **Expert** views the job list and job detail.
2. **Expert** creates and submits a proposal.
3. **Client** reviews the proposals.
4. **Client** accepts one proposal and proposes an initial deposit amount. *(Job Post status: "IN-PROGRESS", Proposal status: "ACCEPTED")*
5. **Expert** reviews and recommends adjusting or confirms the proposed deposit.
6. **System** creates the project and an agreement snapshot.
7. **Client** uploads the contract document (PDF file). *(Note: The system does not verify legal validity).*
8. **Expert** reviews and confirms the uploaded contract (both parties must confirm).
9. The project is ready for milestone payment.

### Exceptions / Alternative Flows:
* **At Step 2 (Invalid Proposal):** If the proposal is invalid or a duplicate -> The **Expert** edits and resubmits it.
* **At Step 3 (Client Rejection/Clarification):** * If the **Client** rejects the proposal -> The job remains OPEN.
    * If the **Client** shortlists or requests clarification -> The **System** updates the proposal status.
* **At Step 5 (Deposit Negotiation):** If the **Expert** adjusts the deposit -> The **Client** must review and agree to the new amount.
* **At Step 8 (Contract Not Confirmed/Uploaded):** If no contract is uploaded or the **Expert** does not confirm -> The **System** proceeds with the agreement snapshot only -> This comes with a limitation/evidence warning.
* **At any step between 2-4 (Proposal Updates):** If the **Expert** adjusts the proposal, the flow in view of expert returns to step 2.

---

## Business Flow 3: Project Management
**Actors:** Client, System, Expert, Admin

### Main Flow (Payment & Work):
1. **Client** selects, creates, or edits a milestone. *(Note: Only the Client has the permission to edit milestones during the project).*
2. **System** prepares the milestone payment order (Deposit + Full Payment for the expert).
3. **Client** initiates the transfer using the system's simulated fake wallet.
4. **Expert** receives the funds in their simulated wallet and confirms receipt.
5. **Expert** starts working. *(Optional: AI assists or monitors throughout the project process; System sends email notifications for major updates).*
6. **Expert** submits the deliverable.
7. **Client** reviews the deliverable.

### Split: Deliverable & Resolution
* **Branch 7A - Approve:** * **Client** approves the deliverable.
    * Milestone is completed.
    * **Expert** is paid in full.
    * **System** processes the simulated transfer and automatically deducts a 10% platform commission during the transaction. *(Note: Deducted from the Expert's earnings when the Client pays in full).*
    * If all milestones are completed $\rightarrow$ Project finishes. Otherwise $\rightarrow$ Loops back to Step 1 for the next milestone.
    * **Client** reviews the **Expert**.

* **Branch 7B - Request Revision:**
    * **Client** requests a revision.
    * **Expert** revises the work.
    * **Expert** resubmits the deliverable $\rightarrow$ Loops back to Step 7.

* **Branch Exception - Dispute:**
    * **Client** or **Expert** flags the milestone.
    * **System** updates the milestone status to simply show as **"Disputed"** (Tranh chấp) or **"Not Disputed"** (Không có tranh chấp).
    * Further action requires external settlement or mutual cancellation, as the platform only flags the status. 

### Exceptions / Alternative Flows:
* **At Step 3 (Simulated Payment Failed):** The simulated transfer fails -> The **Client** must retry the payment in the fake wallet.
* **At Step 6 (Late/Missing Deliverable):** If the deliverable is late or missing -> The **Client** may remind the **Expert** via Chat, or modify the milestones.
* **At Branch Exception (Dispute Timeout):** If the dispute is not confirmed solved after 7 days, the project is closed. 
* **At any step (Dispute Available):** If the dispute is opened, the flow turns into Branch Exception.
* **Review Timeout:** At the end of the project, if no review is submitted -> The project closes without a review.