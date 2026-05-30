# API_CONTRACT.md

# AITasker API Contract

Version: `v1`  
Base path: `/api/v1`  
Format: REST JSON API  
Auth: JWT Bearer Token  
Primary roles: `CLIENT`, `EXPERT`, `ADMIN`

---

## 1. Purpose

This document defines the backend API contract for **AITasker**, an AI-assisted marketplace platform for AI automation services.

The contract supports the MVP flow:

```text
Client requirement
→ AI-assisted job clarification
→ Job publishing
→ Expert recommendation
→ Proposal submission
→ Proposal acceptance
→ Project creation
→ Milestone funding with simulated escrow
→ Deliverable submission
→ Client approval / revision / dispute
→ Payment release / refund / freeze
→ Review
```

---

## 2. Global API Rules

### 2.1 Base URL

```http
/api/v1
```

Example:

```http
POST /api/v1/auth/login
```

### 2.2 Content Type

All request and response bodies use JSON unless stated otherwise.

```http
Content-Type: application/json
Accept: application/json
```

### 2.3 Authentication Header

Protected endpoints require:

```http
Authorization: Bearer <accessToken>
```

### 2.4 ID Format

All entity IDs are UUID strings.

```json
{
  "id": "4cf66f9e-0a8d-4c4b-ae5d-70a8818f8343"
}
```

### 2.5 Date and Time Format

Use ISO-8601.

```json
{
  "createdAt": "2026-05-23T10:30:00Z",
  "deadline": "2026-06-15"
}
```

### 2.6 Currency

Default currency is `AICOIN`.

```json
{
  "amount": 500.0,
  "currency": "AICOIN"
}
```

### 2.7 Standard Success Response

For single resource:

```json
{
  "success": true,
  "data": {},
  "message": "OK"
}
```

For list:

```json
{
  "success": true,
  "data": [],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 100,
    "totalPages": 5
  },
  "message": "OK"
}
```

### 2.8 Standard Error Response

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid request payload",
    "details": [
      {
        "field": "email",
        "message": "Email is required"
      }
    ]
  }
}
```

### 2.9 Common HTTP Status Codes

|  Code | Meaning                                      |
| ----: | -------------------------------------------- |
| `200` | Success                                      |
| `201` | Created                                      |
| `400` | Bad request / validation error               |
| `401` | Unauthorized                                 |
| `403` | Forbidden                                    |
| `404` | Resource not found                           |
| `409` | Business conflict / invalid state transition |
| `500` | Internal server error                        |

---

## 3. Role Permission Summary

| Module                     |                CLIENT |                EXPERT |          ADMIN |
| -------------------------- | --------------------: | --------------------: | -------------: |
| Register/Login             |                   Yes |                   Yes |            Yes |
| Create job                 |                   Yes |                    No |             No |
| Use AI Job Assistant       |                   Yes |                    No | Admin optional |
| View open jobs             |                   Yes |                   Yes |            Yes |
| Submit proposal            |                    No |                   Yes |             No |
| Accept proposal            |          Yes, own job |                    No |             No |
| View project               |           Own project |           Own project |            Yes |
| Fund milestone             |      Yes, own project |                    No |             No |
| Submit deliverable         |                    No |      Yes, own project |             No |
| Approve/reject deliverable |      Yes, own project |                    No |             No |
| Open dispute               |                   Yes |                   Yes |            Yes |
| Resolve dispute            |                    No |                    No |            Yes |
| Write review               | Own completed project | Own completed project |             No |
| Manage users               |                    No |                    No |            Yes |

---

## 4. Status Enums

### 4.1 User Role

```text
CLIENT
EXPERT
ADMIN
```

### 4.2 User Status

```text
ACTIVE
SUSPENDED
DELETED
```

### 4.3 Job Status

```text
DRAFT
OPEN
IN_PROGRESS
COMPLETED
CANCELLED
CLOSED
```

### 4.4 AI Job Suggestion Status

```text
GENERATED
ACCEPTED
REJECTED
FAILED
```

### 4.5 Proposal Status

```text
DRAFTED
SUBMITTED
SHORTLISTED
ACCEPTED
REJECTED
REMOVED
```

### 4.6 Project Status

```text
PENDING
ACTIVE
CANCELED
COMPLETED
```

### 4.7 Milestone Status

```text
CREATED
FUNDED
IN_PROGRESS
SUBMITTED
REVISION_REQUESTED
APPROVED
DISPUTED
PAID
REFUNDED
```

### 4.8 Payment Status

```text
PENDING
HELD
RELEASED
REFUNDED
FROZEN
FAILED
```

### 4.9 Deliverable Status

```text
SUBMITTED
APPROVED
REVISION_REQUESTED
REJECTED
```

### 4.10 Dispute Status

```text
OPEN
UNDER_REVIEW
RESOLVED
CANCELED
```

### 4.11 Dispute Resolution Type

```text
RELEASE_TO_EXPERT
REFUND_TO_CLIENT
SPLIT_PAYMENT
REQUEST_REVISION
```

---

# 5. Authentication APIs

## 5.1 Register

```http
POST /auth/register
```

Public endpoint.

### Request

```json
{
  "email": "client@test.com",
  "password": "Password123!",
  "fullName": "Demo Client",
  "role": "CLIENT"
}
```

### Validation

| Field      | Rule                                |
| ---------- | ----------------------------------- |
| `email`    | Required, valid email, unique       |
| `password` | Required                            |
| `fullName` | Required                            |
| `role`     | Required, one of `CLIENT`, `EXPERT` |

### Response `201`

```json
{
  "success": true,
  "message": "Account created successfully",
  "data": {
    "accessToken": "jwt-access-token",
    "refreshToken": "refresh-token-string",
    "userId": "uuid",
    "email": "client@test.com",
    "role": "CLIENT"
  },
  "timestampUtc": "2026-05-30T10:00:00Z"
}
```

---

## 5.2 Login

```http
POST /auth/login
```

Public endpoint.

### Request

```json
{
  "email": "client@test.com",
  "password": "Password123!"
}
```

### Response `200`

```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "accessToken": "jwt-access-token",
    "refreshToken": "refresh-token-string",
    "userId": "uuid",
    "email": "client@test.com",
    "role": "CLIENT"
  },
  "timestampUtc": "2026-05-30T10:00:00Z"
}
```

---

## 5.3 Get Current User

```http
GET /auth/me
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "email": "client@test.com",
    "fullName": "Demo Client",
    "avatarUrl": null,
    "phone": null,
    "role": "CLIENT",
    "status": "ACTIVE",
    "lastLoginAt": "2026-05-23T10:30:00Z"
  },
  "message": "OK"
}
```

---

## 5.4 Refresh Token

```http
POST /auth/refresh
```

### Request

```json
{
  "refreshToken": "jwt-refresh-token"
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "accessToken": "new-jwt-access-token",
    "refreshToken": "new-jwt-refresh-token",
    "expiresIn": 3600
  },
  "message": "Token refreshed"
}
```

---

## 5.5 Logout

```http
POST /auth/logout
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": null,
  "message": "Logged out successfully"
}
```

---

# 6. Profile APIs

## 6.1 Update Current User

```http
PUT /users/me
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Request

```json
{
  "fullName": "Updated Name",
  "avatarUrl": "https://example.com/avatar.png",
  "phone": "+84901234567"
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "email": "client@test.com",
    "fullName": "Updated Name",
    "avatarUrl": "https://example.com/avatar.png",
    "phone": "+84901234567",
    "role": "CLIENT",
    "status": "ACTIVE"
  },
  "message": "Profile updated"
}
```

---

## 6.2 Get Client Profile

```http
GET /clients/me/profile
```

Roles: `CLIENT`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "userId": "uuid",
    "companyName": "Beauty Shop Demo",
    "industry": "E-commerce",
    "companySize": "1-10",
    "website": "https://example.com",
    "description": "Small e-commerce shop"
  },
  "message": "OK"
}
```

---

## 6.3 Update Client Profile

```http
PUT /clients/me/profile
```

Roles: `CLIENT`

### Request

```json
{
  "companyName": "Beauty Shop Demo",
  "industry": "E-commerce",
  "companySize": "1-10",
  "website": "https://example.com",
  "description": "Small e-commerce shop selling cosmetic products"
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "userId": "uuid",
    "companyName": "Beauty Shop Demo",
    "industry": "E-commerce",
    "companySize": "1-10",
    "website": "https://example.com",
    "description": "Small e-commerce shop selling cosmetic products"
  },
  "message": "Client profile updated"
}
```

---

## 6.4 Get Expert Profile

```http
GET /experts/me/profile
```

Roles: `EXPERT`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "userId": "uuid",
    "title": "AI Chatbot & RAG Developer",
    "bio": "I build AI chatbots and automation workflows.",
    "hourlyRate": 25.0,
    "experienceYears": 3,
    "availabilityStatus": "AVAILABLE",
    "ratingAvg": 4.8,
    "completedProjects": 12,
    "successRate": 95.0,
    "responseTimeMinutes": 120
  },
  "message": "OK"
}
```

---

## 6.5 Update Expert Profile

```http
PUT /experts/me/profile
```

Roles: `EXPERT`

### Request

```json
{
  "title": "AI Chatbot & RAG Developer",
  "bio": "I build AI chatbots, RAG systems, and automation workflows.",
  "hourlyRate": 25.0,
  "experienceYears": 3,
  "availabilityStatus": "AVAILABLE",
  "responseTimeMinutes": 120
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "userId": "uuid",
    "title": "AI Chatbot & RAG Developer",
    "bio": "I build AI chatbots, RAG systems, and automation workflows.",
    "hourlyRate": 25.0,
    "experienceYears": 3,
    "availabilityStatus": "AVAILABLE",
    "ratingAvg": 4.8,
    "completedProjects": 12,
    "successRate": 95.0,
    "responseTimeMinutes": 120
  },
  "message": "Expert profile updated"
}
```

---

## 6.6 Get Expert Public Detail

```http
GET /experts/{expertId}
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "fullName": "Demo Expert",
    "avatarUrl": null,
    "title": "AI Chatbot & RAG Developer",
    "bio": "I build AI chatbots, RAG systems, and automation workflows.",
    "hourlyRate": 25.0,
    "experienceYears": 3,
    "availabilityStatus": "AVAILABLE",
    "ratingAvg": 4.8,
    "completedProjects": 12,
    "successRate": 95.0,
    "skills": [
      {
        "id": "uuid",
        "name": "RAG",
        "level": "ADVANCED",
        "yearsExperience": 2
      }
    ]
  },
  "message": "OK"
}
```

---

# 7. Category and Skill APIs

## 7.1 Get Categories

```http
GET /categories
```

Roles: public or authenticated

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "name": "Chatbot",
      "description": "AI chatbot development and integration",
      "parentId": null
    }
  ],
  "message": "OK"
}
```

---

## 7.2 Get Skills

```http
GET /skills?search=rag&categoryId=uuid
```

Roles: public or authenticated

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "name": "RAG",
      "categoryId": "uuid"
    }
  ],
  "message": "OK"
}
```

---

## 7.3 Add Expert Skill

```http
POST /experts/me/skills
```

Roles: `EXPERT`

### Request

```json
{
  "skillId": "uuid",
  "level": "ADVANCED",
  "yearsExperience": 2
}
```

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "expertId": "uuid",
    "skillId": "uuid",
    "name": "RAG",
    "level": "ADVANCED",
    "yearsExperience": 2
  },
  "message": "Skill added"
}
```

---

## 7.4 Remove Expert Skill

```http
DELETE /experts/me/skills/{skillId}
```

Roles: `EXPERT`

### Response `200`

```json
{
  "success": true,
  "data": null,
  "message": "Skill removed"
}
```

---

# 8. AI Assistant APIs

## 8.1 Generate AI Job Suggestion

```http
POST /ai/job-assistant
```

Roles: `CLIENT`

This endpoint receives a rough client requirement and returns a structured job suggestion.

### Request

```json
{
  "rawInput": "Tôi muốn làm chatbot tư vấn sản phẩm cho shop mỹ phẩm.",
  "businessDomain": "E-commerce",
  "expectedOutcome": "Customers can ask about products and receive recommendations",
  "budgetMin": 500,
  "budgetMax": 1500,
  "timelineDays": 30
}
```

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "jobId": null,
    "clientId": "uuid",
    "rawInput": "Tôi muốn làm chatbot tư vấn sản phẩm cho shop mỹ phẩm.",
    "suggestedTitle": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation",
    "suggestedDescription": "Build a Vietnamese chatbot that can answer product questions, recommend products based on customer needs, and integrate with the shop's product data.",
    "suggestedBudgetMin": 800,
    "suggestedBudgetMax": 1500,
    "suggestedTimelineDays": 30,
    "suggestedSkills": [
      "OpenAI API",
      "RAG",
      "Chatbot",
      "Vector Database",
      "React"
    ],
    "suggestedMilestones": [
      {
        "title": "Requirement analysis and product data preparation",
        "description": "Clarify chatbot scope and prepare product data.",
        "amount": 300,
        "dueDays": 7,
        "acceptanceCriteria": "Product data format is confirmed and chatbot use cases are documented."
      },
      {
        "title": "Chatbot prototype",
        "description": "Build a working chatbot prototype with Vietnamese support.",
        "amount": 700,
        "dueDays": 14,
        "acceptanceCriteria": "Chatbot can answer product questions using provided data."
      }
    ],
    "clarifyingQuestions": [
      "Do you already have product data in CSV, database, or API?",
      "Should the chatbot support Vietnamese only or multiple languages?"
    ],
    "riskWarnings": [
      "Product recommendation quality depends on product data quality.",
      "Integration complexity may increase budget and timeline."
    ],
    "aiModel": "configured-model-name",
    "status": "GENERATED",
    "createdAt": "2026-05-23T10:30:00Z"
  },
  "message": "AI job suggestion generated"
}
```

### Business Rules

- AI output must not be published automatically.
- Client must review and confirm before creating or updating a job.
- If AI provider fails, return `500` or fallback response with `status = FAILED`.

---

## 8.2 Accept AI Job Suggestion into Job Draft

```http
POST /ai/job-assistant/{suggestionId}/accept
```

Roles: `CLIENT`

This endpoint creates a draft job from an AI suggestion.

### Request

```json
{
  "categoryId": "uuid",
  "selectedSkillIds": ["uuid", "uuid"]
}
```

### Response `201`

```json
{
  "success": true,
  "data": {
    "job": {
      "id": "uuid",
      "clientId": "uuid",
      "categoryId": "uuid",
      "title": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation",
      "originalDescription": "Tôi muốn làm chatbot tư vấn sản phẩm cho shop mỹ phẩm.",
      "enhancedDescription": "Build a Vietnamese chatbot that can answer product questions...",
      "businessDomain": "E-commerce",
      "expectedOutcome": "Customers can ask about products and receive recommendations",
      "budgetType": "FIXED",
      "budgetMin": 800,
      "budgetMax": 1500,
      "currency": "USD",
      "timelineDays": 30,
      "status": "DRAFT",
      "visibility": "PUBLIC"
    }
  },
  "message": "Job draft created from AI suggestion"
}
```

---

## 8.3 Reject AI Job Suggestion

```http
POST /ai/job-assistant/{suggestionId}/reject
```

Roles: `CLIENT`

### Request

```json
{
  "reason": "The suggested budget is too high"
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "REJECTED"
  },
  "message": "AI suggestion rejected"
}
```

---

## 8.4 Generate AI Service Description

```http
POST /ai/service-generator
```

Roles: `EXPERT`

Optional MVP endpoint.

### Request

```json
{
  "rawInput": "Tôi làm chatbot bằng OpenAI API, RAG, deploy web.",
  "skills": ["OpenAI API", "RAG", "React", "FastAPI"],
  "priceFrom": 300,
  "deliveryDays": 14
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "suggestedTitle": "Custom RAG Chatbot Development for Business Websites",
    "suggestedDescription": "I will build a chatbot that answers questions from your business documents and integrates with your website.",
    "packages": [
      {
        "name": "Basic",
        "price": 300,
        "deliveryDays": 7,
        "description": "Prototype chatbot with sample data"
      },
      {
        "name": "Standard",
        "price": 800,
        "deliveryDays": 14,
        "description": "RAG chatbot with document upload and web integration"
      },
      {
        "name": "Premium",
        "price": 1500,
        "deliveryDays": 30,
        "description": "Full chatbot integration with admin dashboard"
      }
    ],
    "faqs": [
      {
        "question": "What data do you need?",
        "answer": "I need product documents, FAQs, or website content."
      }
    ]
  },
  "message": "AI service description generated"
}
```

---

# 9. Job APIs

## 9.1 Create Job

```http
POST /jobs
```

Roles: `CLIENT`

### Request

```json
{
  "categoryId": "uuid",
  "title": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation",
  "originalDescription": "Tôi muốn chatbot tư vấn sản phẩm cho shop.",
  "enhancedDescription": "Build a Vietnamese chatbot that can answer product questions and recommend products.",
  "businessDomain": "E-commerce",
  "expectedOutcome": "Customers can receive product suggestions from chatbot",
  "budgetType": "FIXED",
  "budgetMin": 800,
  "budgetMax": 1500,
  "currency": "USD",
  "timelineDays": 30,
  "deadline": "2026-06-30",
  "experienceLevel": "INTERMEDIATE",
  "visibility": "PUBLIC",
  "skillIds": ["uuid", "uuid"]
}
```

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "clientId": "uuid",
    "categoryId": "uuid",
    "title": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation",
    "originalDescription": "Tôi muốn chatbot tư vấn sản phẩm cho shop.",
    "enhancedDescription": "Build a Vietnamese chatbot that can answer product questions and recommend products.",
    "businessDomain": "E-commerce",
    "expectedOutcome": "Customers can receive product suggestions from chatbot",
    "budgetType": "FIXED",
    "budgetMin": 800,
    "budgetMax": 1500,
    "currency": "USD",
    "timelineDays": 30,
    "deadline": "2026-06-30",
    "experienceLevel": "INTERMEDIATE",
    "status": "DRAFT",
    "visibility": "PUBLIC",
    "skills": [
      {
        "id": "uuid",
        "name": "RAG",
        "isRequired": true
      }
    ],
    "createdAt": "2026-05-23T10:30:00Z",
    "updatedAt": "2026-05-23T10:30:00Z"
  },
  "message": "Job created"
}
```

---

## 9.2 Get Jobs

```http
GET /jobs?status=OPEN&categoryId=uuid&skillId=uuid&search=chatbot&page=1&pageSize=20
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Query Parameters

| Name         | Type   | Required | Description              |
| ------------ | ------ | -------: | ------------------------ |
| `status`     | string |       No | Filter by job status     |
| `categoryId` | UUID   |       No | Filter by category       |
| `skillId`    | UUID   |       No | Filter by skill          |
| `search`     | string |       No | Search title/description |
| `page`       | number |       No | Default `1`              |
| `pageSize`   | number |       No | Default `20`             |

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "clientId": "uuid",
      "clientName": "Demo Client",
      "categoryName": "Chatbot",
      "title": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation",
      "enhancedDescription": "Build a Vietnamese chatbot...",
      "budgetType": "FIXED",
      "budgetMin": 800,
      "budgetMax": 1500,
      "currency": "USD",
      "timelineDays": 30,
      "status": "OPEN",
      "publishedAt": "2026-05-23T10:30:00Z",
      "skills": ["OpenAI API", "RAG", "Chatbot"]
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  },
  "message": "OK"
}
```

---

## 9.3 Get Job Detail

```http
GET /jobs/{jobId}
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "clientId": "uuid",
    "clientName": "Demo Client",
    "categoryId": "uuid",
    "categoryName": "Chatbot",
    "title": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation",
    "originalDescription": "Tôi muốn chatbot tư vấn sản phẩm cho shop.",
    "enhancedDescription": "Build a Vietnamese chatbot...",
    "businessDomain": "E-commerce",
    "expectedOutcome": "Customers can receive product suggestions from chatbot",
    "budgetType": "FIXED",
    "budgetMin": 800,
    "budgetMax": 1500,
    "currency": "USD",
    "timelineDays": 30,
    "deadline": "2026-06-30",
    "experienceLevel": "INTERMEDIATE",
    "status": "OPEN",
    "visibility": "PUBLIC",
    "publishedAt": "2026-05-23T10:30:00Z",
    "skills": [
      {
        "id": "uuid",
        "name": "RAG",
        "isRequired": true
      }
    ],
    "createdAt": "2026-05-23T10:30:00Z",
    "updatedAt": "2026-05-23T10:30:00Z"
  },
  "message": "OK"
}
```

---

## 9.4 Update Job

```http
PUT /jobs/{jobId}
```

Roles: `CLIENT`

Only the job owner can update the job.  
Allowed when job status is `DRAFT` or `OPEN`.

### Request

```json
{
  "categoryId": "uuid",
  "title": "Updated job title",
  "originalDescription": "Updated original requirement",
  "enhancedDescription": "Updated enhanced requirement",
  "businessDomain": "E-commerce",
  "expectedOutcome": "Updated outcome",
  "budgetType": "FIXED",
  "budgetMin": 900,
  "budgetMax": 1600,
  "currency": "USD",
  "timelineDays": 35,
  "deadline": "2026-07-05",
  "experienceLevel": "INTERMEDIATE",
  "visibility": "PUBLIC",
  "skillIds": ["uuid", "uuid"]
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "title": "Updated job title",
    "status": "DRAFT",
    "updatedAt": "2026-05-23T10:45:00Z"
  },
  "message": "Job updated"
}
```

---

## 9.5 Publish Job

```http
POST /jobs/{jobId}/publish
```

Roles: `CLIENT`

### Business Rules

- Only job owner can publish.
- Job must be `DRAFT`.
- Required fields: title, description, budget, timeline or deadline.
- On success, status becomes `OPEN`.

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "OPEN",
    "publishedAt": "2026-05-23T10:50:00Z"
  },
  "message": "Job published"
}
```

---

## 9.6 Cancel Job

```http
POST /jobs/{jobId}/cancel
```

Roles: `CLIENT`

Allowed when job status is `DRAFT` or `OPEN`.

### Request

```json
{
  "reason": "Client no longer needs this job"
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "CANCELLED"
  },
  "message": "Job cancelled"
}
```

---

# 10. Recommendation APIs

## 10.1 Generate Expert Recommendations for Job

```http
POST /jobs/{jobId}/recommendations/generate
```

Roles: `CLIENT`, `ADMIN`

### Business Rules

- Job must exist.
- Client can only generate recommendations for own job.
- Job should be `OPEN`.
- System stores results in `RecommendationResults`.

### Response `201`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "jobId": "uuid",
      "expertId": "uuid",
      "expertName": "Demo Expert",
      "expertTitle": "AI Chatbot & RAG Developer",
      "totalScore": 87.5,
      "skillScore": 35.0,
      "portfolioScore": 20.0,
      "ratingScore": 14.0,
      "budgetScore": 9.0,
      "availabilityScore": 4.5,
      "completionScore": 5.0,
      "explanation": "Matches required RAG and chatbot skills, strong rating, and budget fits client range."
    }
  ],
  "message": "Recommendations generated"
}
```

---

## 10.2 Get Expert Recommendations for Job

```http
GET /jobs/{jobId}/recommendations
```

Roles: `CLIENT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "jobId": "uuid",
      "expertId": "uuid",
      "expertName": "Demo Expert",
      "expertTitle": "AI Chatbot & RAG Developer",
      "ratingAvg": 4.8,
      "completedProjects": 12,
      "totalScore": 87.5,
      "explanation": "Matches required skills and has relevant chatbot/RAG experience."
    }
  ],
  "message": "OK"
}
```

---

# 11. Proposal APIs

## 11.1 Submit Proposal

```http
POST /jobs/{jobId}/proposals
```

Roles: `EXPERT`

### Business Rules

- Job must be `OPEN`.
- Expert cannot submit more than one proposal for the same job.
- Expert cannot submit proposal to own job if the same user is both client and expert.
- Proposed budget must be greater than or equal to `0`.

### Request

```json
{
  "coverLetter": "I can build this chatbot using RAG and OpenAI API. I have experience with e-commerce chatbots.",
  "proposedBudget": 1200,
  "proposedTimelineDays": 30,
  "currency": "USD",
  "milestones": [
    {
      "title": "Requirement analysis and data preparation",
      "description": "Clarify requirements and prepare product data.",
      "amount": 300,
      "dueDays": 7,
      "acceptanceCriteria": "Use cases and product data format are confirmed.",
      "orderIndex": 1
    },
    {
      "title": "Chatbot prototype",
      "description": "Build working prototype.",
      "amount": 900,
      "dueDays": 23,
      "acceptanceCriteria": "Chatbot answers product questions in Vietnamese.",
      "orderIndex": 2
    }
  ]
}
```

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "jobId": "uuid",
    "expertId": "uuid",
    "coverLetter": "I can build this chatbot using RAG and OpenAI API.",
    "proposedBudget": 1200,
    "proposedTimelineDays": 30,
    "currency": "USD",
    "status": "SUBMITTED",
    "milestones": [
      {
        "id": "uuid",
        "title": "Requirement analysis and data preparation",
        "amount": 300,
        "dueDays": 7,
        "orderIndex": 1
      }
    ],
    "submittedAt": "2026-05-23T11:00:00Z"
  },
  "message": "Proposal submitted"
}
```

---

## 11.2 Get Proposals for Job

```http
GET /jobs/{jobId}/proposals
```

Roles: `CLIENT`, `ADMIN`

### Business Rules

- Client can only view proposals for own job.
- Admin can view all proposals.

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "jobId": "uuid",
      "expertId": "uuid",
      "expertName": "Demo Expert",
      "expertTitle": "AI Chatbot & RAG Developer",
      "expertRating": 4.8,
      "coverLetter": "I can build this chatbot...",
      "proposedBudget": 1200,
      "proposedTimelineDays": 30,
      "currency": "USD",
      "status": "SUBMITTED",
      "submittedAt": "2026-05-23T11:00:00Z"
    }
  ],
  "message": "OK"
}
```

---

## 11.3 Get Proposal Detail

```http
GET /proposals/{proposalId}
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Business Rules

- Client can view proposals submitted to own job.
- Expert can view own proposal.
- Admin can view all.

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "jobId": "uuid",
    "expertId": "uuid",
    "expertName": "Demo Expert",
    "coverLetter": "I can build this chatbot...",
    "proposedBudget": 1200,
    "proposedTimelineDays": 30,
    "currency": "USD",
    "status": "SUBMITTED",
    "milestones": [
      {
        "id": "uuid",
        "title": "Chatbot prototype",
        "description": "Build working prototype",
        "amount": 900,
        "dueDays": 23,
        "acceptanceCriteria": "Chatbot answers product questions in Vietnamese.",
        "orderIndex": 2
      }
    ],
    "submittedAt": "2026-05-23T11:00:00Z",
    "updatedAt": "2026-05-23T11:00:00Z"
  },
  "message": "OK"
}
```

---

## 11.4 Shortlist Proposal

```http
PUT /proposals/{proposalId}/shortlist
```

Roles: `CLIENT`

### Business Rules

- Client must own the job.
- Proposal must be `SUBMITTED`.
- Proposal becomes `SHORTLISTED`.

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "SHORTLISTED",
    "updatedAt": "2026-05-23T11:10:00Z"
  },
  "message": "Proposal shortlisted"
}
```

---

## 11.5 Reject Proposal

```http
PUT /proposals/{proposalId}/reject
```

Roles: `CLIENT`

### Request

```json
{
  "reason": "Budget is higher than expected"
}
```

### Business Rules

- Client must own the job.
- Proposal must be `SUBMITTED` or `SHORTLISTED`.
- Proposal becomes `REJECTED`.

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "REJECTED",
    "updatedAt": "2026-05-23T11:10:00Z"
  },
  "message": "Proposal rejected"
}
```

---

## 11.6 Withdraw Proposal

```http
PUT /proposals/{proposalId}/withdraw
```

Roles: `EXPERT`

### Business Rules

- Expert must own the proposal.
- Proposal must be `SUBMITTED` or `SHORTLISTED`.
- Proposal becomes `WITHDRAWN`.

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "WITHDRAWN",
    "withdrawnAt": "2026-05-23T11:15:00Z"
  },
  "message": "Proposal withdrawn"
}
```

---

## 11.7 Accept Proposal

```http
PUT /proposals/{proposalId}/accept
```

Roles: `CLIENT`

### Business Rules

- Client must own the job.
- Job must be `OPEN`.
- Proposal must be `SUBMITTED` or `SHORTLISTED`.
- Accepted proposal becomes `ACCEPTED`.
- Other proposals of the same job become `REJECTED`.
- Job becomes `IN_PROGRESS`.
- Project is created with status `PENDING_PAYMENT`.
- Project milestones are created from proposal milestones.
- The operation must be atomic/transactional.

### Response `201`

```json
{
  "success": true,
  "data": {
    "proposal": {
      "id": "uuid",
      "status": "ACCEPTED"
    },
    "job": {
      "id": "uuid",
      "status": "IN_PROGRESS"
    },
    "project": {
      "id": "uuid",
      "jobId": "uuid",
      "acceptedProposalId": "uuid",
      "clientId": "uuid",
      "expertId": "uuid",
      "title": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation",
      "description": "Build a Vietnamese chatbot...",
      "totalBudget": 1200,
      "currency": "USD",
      "status": "PENDING_PAYMENT",
      "createdAt": "2026-05-23T11:20:00Z"
    }
  },
  "message": "Proposal accepted and project created"
}
```

---

# 12. Project APIs

## 12.1 Get Projects

```http
GET /projects?status=ACTIVE&page=1&pageSize=20
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Business Rules

- Client sees own projects.
- Expert sees assigned projects.
- Admin sees all projects.

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "jobId": "uuid",
      "acceptedProposalId": "uuid",
      "clientId": "uuid",
      "clientName": "Demo Client",
      "expertId": "uuid",
      "expertName": "Demo Expert",
      "title": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation",
      "totalBudget": 1200,
      "currency": "USD",
      "status": "PENDING",
      "createdAt": "2026-05-23T11:20:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  },
  "message": "OK"
}
```

---

## 12.2 Get Project Detail

```http
GET /projects/{projectId}
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "jobId": "uuid",
    "acceptedProposalId": "uuid",
    "clientId": "uuid",
    "clientName": "Demo Client",
    "expertId": "uuid",
    "expertName": "Demo Expert",
    "title": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation",
    "description": "Build a Vietnamese chatbot...",
    "totalBudget": 1200,
    "currency": "USD",
    "status": "PENDING",
    "startDate": null,
    "endDate": null,
    "completedAt": null,
    "milestones": [
      {
        "id": "uuid",
        "title": "Requirement analysis and data preparation",
        "amount": 300,
        "currency": "USD",
        "dueDate": "2026-06-01",
        "status": "CREATED",
        "orderIndex": 1
      }
    ],
    "createdAt": "2026-05-23T11:20:00Z",
    "updatedAt": "2026-05-23T11:20:00Z"
  },
  "message": "OK"
}
```

---

## 12.3 Cancel Project

```http
PUT /projects/{projectId}/cancel
```

Roles: `CLIENT`, `ADMIN`

### Request

```json
{
  "reason": "Project cancelled before any milestone was funded"
}
```

### Business Rules

- Client can cancel only own project.
- Allowed only if no milestone has payment status `HELD`, `FROZEN`, or `RELEASED`.
- Project becomes `CANCELLED`.

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "CANCELLED",
    "cancelledAt": "2026-05-23T11:30:00Z"
  },
  "message": "Project cancelled"
}
```

---

# 13. Milestone APIs

## 13.1 Create Milestone

```http
POST /projects/{projectId}/milestones
```

Roles: `CLIENT`

Usually milestones are created when accepting a proposal.  
This endpoint is for adding additional milestones before funding.

### Request

```json
{
  "title": "Website integration",
  "description": "Integrate chatbot widget into website.",
  "acceptanceCriteria": "Chatbot widget appears on website and can answer product questions.",
  "amount": 500,
  "currency": "USD",
  "dueDate": "2026-06-20",
  "orderIndex": 3
}
```

### Business Rules

- Project must belong to client.
- Project must not be `COMPLETED` or `CANCELLED`.
- Amount must be greater than or equal to `0`.
- New milestone starts as `CREATED`.

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "projectId": "uuid",
    "title": "Website integration",
    "description": "Integrate chatbot widget into website.",
    "acceptanceCriteria": "Chatbot widget appears on website and can answer product questions.",
    "amount": 500,
    "currency": "USD",
    "dueDate": "2026-06-20",
    "orderIndex": 3,
    "status": "CREATED",
    "createdAt": "2026-05-23T11:35:00Z"
  },
  "message": "Milestone created"
}
```

---

## 13.2 Update Milestone

```http
PUT /milestones/{milestoneId}
```

Roles: `CLIENT`

### Business Rules

- Client must own the project.
- Milestone must be `CREATED`.
- Funded or submitted milestones cannot be edited directly.

### Request

```json
{
  "title": "Updated milestone title",
  "description": "Updated milestone description",
  "acceptanceCriteria": "Updated acceptance criteria",
  "amount": 550,
  "currency": "USD",
  "dueDate": "2026-06-22",
  "orderIndex": 3
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "title": "Updated milestone title",
    "amount": 550,
    "status": "CREATED",
    "updatedAt": "2026-05-23T11:40:00Z"
  },
  "message": "Milestone updated"
}
```

---

## 13.3 Fund Milestone

```http
PUT /milestones/{milestoneId}/fund
```

Roles: `CLIENT`

### Business Rules

- Client must own the project.
- Milestone must be `CREATED`.
- Client wallet available balance must be greater than or equal to milestone amount.
- System creates `Payment` with status `HELD`.
- Client wallet available balance decreases.
- Client wallet held balance increases.
- Milestone becomes `FUNDED`.
- Project becomes `ACTIVE` if it was `PENDING_PAYMENT`.
- Operation must be atomic/transactional.

### Response `200`

```json
{
  "success": true,
  "data": {
    "milestone": {
      "id": "uuid",
      "status": "FUNDED",
      "fundedAt": "2026-05-23T11:45:00Z"
    },
    "payment": {
      "id": "uuid",
      "projectId": "uuid",
      "milestoneId": "uuid",
      "payerId": "uuid",
      "payeeId": "uuid",
      "amount": 300,
      "currency": "USD",
      "status": "HELD",
      "heldAt": "2026-05-23T11:45:00Z"
    },
    "wallet": {
      "availableBalance": 1700,
      "heldBalance": 300,
      "currency": "USD"
    }
  },
  "message": "Milestone funded"
}
```

---

## 13.4 Request Revision

```http
PUT /milestones/{milestoneId}/request-revision
```

Roles: `CLIENT`

### Request

```json
{
  "reason": "The chatbot does not answer from the uploaded product data.",
  "requiredChanges": "Please fix retrieval logic and add source references."
}
```

### Business Rules

- Milestone must be `SUBMITTED`.
- Payment remains `HELD`.
- Milestone becomes `REVISION_REQUESTED`.
- Latest deliverable becomes `REVISION_REQUESTED`.

### Response `200`

```json
{
  "success": true,
  "data": {
    "milestone": {
      "id": "uuid",
      "status": "REVISION_REQUESTED"
    },
    "latestDeliverable": {
      "id": "uuid",
      "status": "REVISION_REQUESTED"
    }
  },
  "message": "Revision requested"
}
```

---

## 13.5 Approve Milestone

```http
PUT /milestones/{milestoneId}/approve
```

Roles: `CLIENT`

### Business Rules

- Client must own the project.
- Milestone must be `SUBMITTED`.
- Payment must be `HELD`.
- Payment becomes `RELEASED`.
- Milestone becomes `PAID`.
- Latest deliverable becomes `APPROVED`.
- Client held balance decreases.
- Expert available balance and total earned increase.
- If all milestones are `PAID`, project becomes `COMPLETED`.
- Operation must be atomic/transactional.

### Response `200`

```json
{
  "success": true,
  "data": {
    "milestone": {
      "id": "uuid",
      "status": "PAID",
      "approvedAt": "2026-05-23T12:00:00Z",
      "paidAt": "2026-05-23T12:00:00Z"
    },
    "payment": {
      "id": "uuid",
      "status": "RELEASED",
      "releasedAt": "2026-05-23T12:00:00Z"
    },
    "project": {
      "id": "uuid",
      "status": "ACTIVE"
    }
  },
  "message": "Milestone approved and payment released"
}
```

---

## 13.6 Open Dispute for Milestone

```http
POST /milestones/{milestoneId}/dispute
```

Roles: `CLIENT`, `EXPERT`

### Request

```json
{
  "reason": "Deliverable does not meet acceptance criteria",
  "description": "The chatbot cannot answer based on the provided product data.",
  "againstUserId": "uuid"
}
```

### Business Rules

- Milestone must have a related payment.
- Payment must be `HELD`.
- Payment becomes `FROZEN`.
- Milestone becomes `DISPUTED`.
- Project becomes `DISPUTED`.
- Dispute status starts as `OPEN`.

### Response `201`

```json
{
  "success": true,
  "data": {
    "dispute": {
      "id": "uuid",
      "projectId": "uuid",
      "milestoneId": "uuid",
      "paymentId": "uuid",
      "openedBy": "uuid",
      "againstUserId": "uuid",
      "reason": "Deliverable does not meet acceptance criteria",
      "description": "The chatbot cannot answer based on the provided product data.",
      "status": "OPEN",
      "createdAt": "2026-05-23T12:10:00Z"
    },
    "payment": {
      "id": "uuid",
      "status": "FROZEN"
    },
    "milestone": {
      "id": "uuid",
      "status": "DISPUTED"
    },
    "project": {
      "id": "uuid",
      "status": "DISPUTED"
    }
  },
  "message": "Dispute opened"
}
```

---

# 14. Deliverable APIs

## 14.1 Submit Deliverable

```http
POST /milestones/{milestoneId}/deliverables
```

Roles: `EXPERT`

### Request

```json
{
  "description": "Submitted chatbot prototype with demo URL and source code.",
  "fileUrl": "https://drive.google.com/file/example",
  "demoUrl": "https://demo.example.com",
  "sourceCodeUrl": "https://github.com/example/repo",
  "note": "Please test Vietnamese product questions."
}
```

### Business Rules

- Expert must own the project.
- Milestone must be `FUNDED`, `IN_PROGRESS`, or `REVISION_REQUESTED`.
- New deliverable status is `SUBMITTED`.
- Milestone becomes `SUBMITTED`.
- Project can become `IN_REVIEW`.
- `revisionNumber` increments for re-submission.

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "milestoneId": "uuid",
    "expertId": "uuid",
    "description": "Submitted chatbot prototype with demo URL and source code.",
    "fileUrl": "https://drive.google.com/file/example",
    "demoUrl": "https://demo.example.com",
    "sourceCodeUrl": "https://github.com/example/repo",
    "note": "Please test Vietnamese product questions.",
    "revisionNumber": 1,
    "status": "SUBMITTED",
    "submittedAt": "2026-05-23T11:55:00Z"
  },
  "message": "Deliverable submitted"
}
```

---

## 14.2 Get Deliverables by Milestone

```http
GET /milestones/{milestoneId}/deliverables
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "milestoneId": "uuid",
      "expertId": "uuid",
      "description": "Submitted chatbot prototype.",
      "fileUrl": "https://drive.google.com/file/example",
      "demoUrl": "https://demo.example.com",
      "sourceCodeUrl": "https://github.com/example/repo",
      "note": "Please test Vietnamese product questions.",
      "revisionNumber": 1,
      "status": "SUBMITTED",
      "submittedAt": "2026-05-23T11:55:00Z",
      "reviewedAt": null
    }
  ],
  "message": "OK"
}
```

---

# 15. Wallet and Payment APIs

## 15.1 Get Current Wallet

```http
GET /wallet/me
```

Roles: `CLIENT`, `EXPERT`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "userId": "uuid",
    "availableBalance": 1700,
    "heldBalance": 300,
    "totalEarned": 0,
    "currency": "USD",
    "updatedAt": "2026-05-23T11:45:00Z"
  },
  "message": "OK"
}
```

---

## 15.2 Demo Deposit

```http
POST /wallet/deposit-demo
```

Roles: `CLIENT`

This endpoint simulates adding balance for academic/demo purpose.

### Request

```json
{
  "amount": 2000,
  "currency": "USD",
  "description": "Demo deposit for testing escrow flow"
}
```

### Business Rules

- Amount must be greater than `0`.
- Creates `WalletTransaction` with type `DEMO_DEPOSIT` and direction `CREDIT`.

### Response `200`

```json
{
  "success": true,
  "data": {
    "wallet": {
      "id": "uuid",
      "availableBalance": 2000,
      "heldBalance": 0,
      "totalEarned": 0,
      "currency": "USD"
    },
    "transaction": {
      "id": "uuid",
      "type": "DEMO_DEPOSIT",
      "direction": "CREDIT",
      "amount": 2000,
      "balanceBefore": 0,
      "balanceAfter": 2000,
      "description": "Demo deposit for testing escrow flow",
      "createdAt": "2026-05-23T10:00:00Z"
    }
  },
  "message": "Demo deposit completed"
}
```

---

## 15.3 Get Payment History

```http
GET /payments/history?page=1&pageSize=20&type=ESCROW_HOLD
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "walletId": "uuid",
      "paymentId": "uuid",
      "userId": "uuid",
      "type": "ESCROW_HOLD",
      "direction": "DEBIT",
      "amount": 300,
      "balanceBefore": 2000,
      "balanceAfter": 1700,
      "description": "Funded milestone: Requirement analysis",
      "createdAt": "2026-05-23T11:45:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  },
  "message": "OK"
}
```

---

## 15.4 Request Withdrawal

```http
POST /withdrawals/request
```

Roles: `EXPERT`

Optional MVP endpoint. This is simulated only.

### Request

```json
{
  "amount": 500,
  "currency": "USD",
  "note": "Demo withdrawal request"
}
```

### Business Rules

- Expert available balance must be greater than or equal to amount.
- Creates transaction with type `WITHDRAWAL_REQUEST`.

### Response `201`

```json
{
  "success": true,
  "data": {
    "transactionId": "uuid",
    "type": "WITHDRAWAL_REQUEST",
    "amount": 500,
    "currency": "USD",
    "status": "REQUESTED"
  },
  "message": "Withdrawal request created"
}
```

---

# 16. Review APIs

## 16.1 Create Review

```http
POST /reviews
```

Roles: `CLIENT`, `EXPERT`

### Request

```json
{
  "projectId": "uuid",
  "revieweeId": "uuid",
  "rating": 5,
  "comment": "Great communication and high-quality delivery.",
  "communicationRating": 5,
  "qualityRating": 5,
  "deadlineRating": 4,
  "requirementClarityRating": null
}
```

### Business Rules

- Project must be `COMPLETED`.
- Reviewer must be either project client or project expert.
- Reviewee must be the other party.
- Reviewer cannot review self.
- One review per reviewer-reviewee per project.
- Rating must be from `1` to `5`.
- After review, expert/client rating summary should be recalculated.

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "projectId": "uuid",
    "reviewerId": "uuid",
    "revieweeId": "uuid",
    "rating": 5,
    "comment": "Great communication and high-quality delivery.",
    "communicationRating": 5,
    "qualityRating": 5,
    "deadlineRating": 4,
    "requirementClarityRating": null,
    "createdAt": "2026-05-23T12:30:00Z"
  },
  "message": "Review created"
}
```

---

## 16.2 Get Reviews for User

```http
GET /users/{userId}/reviews?page=1&pageSize=20
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "projectId": "uuid",
      "reviewerId": "uuid",
      "reviewerName": "Demo Client",
      "revieweeId": "uuid",
      "rating": 5,
      "comment": "Great communication and high-quality delivery.",
      "createdAt": "2026-05-23T12:30:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  },
  "message": "OK"
}
```

---

# 17. Messaging APIs

## 17.1 Get Conversations

```http
GET /conversations?page=1&pageSize=20
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "projectId": "uuid",
      "jobId": null,
      "clientId": "uuid",
      "clientName": "Demo Client",
      "expertId": "uuid",
      "expertName": "Demo Expert",
      "lastMessage": "Please test the chatbot demo.",
      "updatedAt": "2026-05-23T12:00:00Z",
      "unreadCount": 1
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  },
  "message": "OK"
}
```

---

## 17.2 Get Conversation Messages

```http
GET /conversations/{conversationId}/messages?page=1&pageSize=50
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "conversationId": "uuid",
      "senderId": "uuid",
      "senderName": "Demo Expert",
      "content": "Please test the chatbot demo.",
      "attachmentUrl": null,
      "isRead": false,
      "createdAt": "2026-05-23T12:00:00Z",
      "readAt": null
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "totalItems": 1,
    "totalPages": 1
  },
  "message": "OK"
}
```

---

## 17.3 Send Message

```http
POST /conversations/{conversationId}/messages
```

Roles: `CLIENT`, `EXPERT`

### Request

```json
{
  "content": "Please test the chatbot demo.",
  "attachmentUrl": null
}
```

### Business Rules

- Sender must be a participant in the conversation.
- `content` or `attachmentUrl` must be provided.

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "conversationId": "uuid",
    "senderId": "uuid",
    "content": "Please test the chatbot demo.",
    "attachmentUrl": null,
    "isRead": false,
    "createdAt": "2026-05-23T12:00:00Z"
  },
  "message": "Message sent"
}
```

---

## 17.4 Mark Messages as Read

```http
PUT /conversations/{conversationId}/read
```

Roles: `CLIENT`, `EXPERT`

### Response `200`

```json
{
  "success": true,
  "data": {
    "conversationId": "uuid",
    "markedReadCount": 5
  },
  "message": "Messages marked as read"
}
```

---

# 18. Dispute APIs

## 18.1 Get Disputes

```http
GET /admin/disputes?status=OPEN&page=1&pageSize=20
```

Roles: `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "projectId": "uuid",
      "milestoneId": "uuid",
      "paymentId": "uuid",
      "openedBy": "uuid",
      "openedByName": "Demo Client",
      "againstUserId": "uuid",
      "reason": "Deliverable does not meet acceptance criteria",
      "status": "OPEN",
      "createdAt": "2026-05-23T12:10:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  },
  "message": "OK"
}
```

---

## 18.2 Get Dispute Detail

```http
GET /admin/disputes/{disputeId}
```

Roles: `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "projectId": "uuid",
    "milestoneId": "uuid",
    "paymentId": "uuid",
    "openedBy": "uuid",
    "openedByName": "Demo Client",
    "againstUserId": "uuid",
    "againstUserName": "Demo Expert",
    "reason": "Deliverable does not meet acceptance criteria",
    "description": "The chatbot cannot answer using the uploaded data.",
    "status": "OPEN",
    "project": {
      "id": "uuid",
      "title": "Build a Vietnamese AI Chatbot for Cosmetic Product Consultation"
    },
    "milestone": {
      "id": "uuid",
      "title": "Chatbot prototype",
      "acceptanceCriteria": "Chatbot answers product questions in Vietnamese."
    },
    "payment": {
      "id": "uuid",
      "amount": 900,
      "currency": "USD",
      "status": "FROZEN"
    },
    "evidence": [
      {
        "id": "uuid",
        "submittedBy": "uuid",
        "submittedByName": "Demo Client",
        "content": "The chatbot returned unrelated answers.",
        "fileUrl": null,
        "createdAt": "2026-05-23T12:15:00Z"
      }
    ],
    "createdAt": "2026-05-23T12:10:00Z",
    "updatedAt": "2026-05-23T12:10:00Z"
  },
  "message": "OK"
}
```

---

## 18.3 Add Dispute Evidence

```http
POST /disputes/{disputeId}/evidence
```

Roles: `CLIENT`, `EXPERT`, `ADMIN`

### Request

```json
{
  "content": "Here is a screenshot showing the chatbot response.",
  "fileUrl": "https://example.com/evidence.png"
}
```

### Business Rules

- User must be project client, project expert, or admin.
- Dispute must be `OPEN` or `UNDER_REVIEW`.

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "disputeId": "uuid",
    "submittedBy": "uuid",
    "content": "Here is a screenshot showing the chatbot response.",
    "fileUrl": "https://example.com/evidence.png",
    "createdAt": "2026-05-23T12:15:00Z"
  },
  "message": "Evidence added"
}
```

---

## 18.4 Resolve Dispute

```http
PUT /admin/disputes/{disputeId}/resolve
```

Roles: `ADMIN`

### Request

```json
{
  "resolutionType": "SPLIT_PAYMENT",
  "resolutionNote": "Expert completed part of the milestone, but some acceptance criteria were not met.",
  "releaseAmount": 500,
  "refundAmount": 400
}
```

### Business Rules

- Dispute must be `OPEN` or `UNDER_REVIEW`.
- Related payment must be `FROZEN`.
- `RELEASE_TO_EXPERT`: full amount goes to expert.
- `REFUND_TO_CLIENT`: full amount returns to client.
- `SPLIT_PAYMENT`: `releaseAmount + refundAmount` must equal payment amount.
- `REQUEST_REVISION`: payment remains `HELD`, milestone becomes `REVISION_REQUESTED`.
- Operation must be atomic/transactional.
- Dispute becomes `RESOLVED`.

### Response `200`

```json
{
  "success": true,
  "data": {
    "dispute": {
      "id": "uuid",
      "status": "RESOLVED",
      "resolutionType": "SPLIT_PAYMENT",
      "resolutionNote": "Expert completed part of the milestone, but some criteria were not met.",
      "resolvedAt": "2026-05-23T12:30:00Z"
    },
    "payment": {
      "id": "uuid",
      "status": "PARTIALLY_RELEASED"
    },
    "milestone": {
      "id": "uuid",
      "status": "PAID"
    },
    "project": {
      "id": "uuid",
      "status": "ACTIVE"
    }
  },
  "message": "Dispute resolved"
}
```

---

# 19. Admin APIs

## 19.1 Get Users

```http
GET /admin/users?role=EXPERT&status=ACTIVE&search=demo&page=1&pageSize=20
```

Roles: `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "email": "expert@test.com",
      "fullName": "Demo Expert",
      "role": "EXPERT",
      "status": "ACTIVE",
      "createdAt": "2026-05-23T10:00:00Z",
      "lastLoginAt": "2026-05-23T10:30:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  },
  "message": "OK"
}
```

---

## 19.2 Suspend User

```http
PUT /admin/users/{userId}/suspend
```

Roles: `ADMIN`

### Request

```json
{
  "reason": "Violation of platform policy"
}
```

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "SUSPENDED"
  },
  "message": "User suspended"
}
```

---

## 19.3 Activate User

```http
PUT /admin/users/{userId}/activate
```

Roles: `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "ACTIVE"
  },
  "message": "User activated"
}
```

---

## 19.4 Admin Dashboard Summary

```http
GET /admin/dashboard/summary
```

Roles: `ADMIN`

### Response `200`

```json
{
  "success": true,
  "data": {
    "totalUsers": 120,
    "totalClients": 60,
    "totalExperts": 55,
    "totalAdmins": 5,
    "totalJobs": 80,
    "openJobs": 25,
    "activeProjects": 18,
    "completedProjects": 30,
    "openDisputes": 3,
    "totalHeldAmount": 5000,
    "totalReleasedAmount": 12000,
    "currency": "USD"
  },
  "message": "OK"
}
```

---

# 20. Service Marketplace APIs

This module is optional for MVP. Use it if the team decides to support expert service packages.

## 20.1 Create Service

```http
POST /services
```

Roles: `EXPERT`

### Request

```json
{
  "title": "RAG Chatbot Development",
  "description": "I will build a custom RAG chatbot for your business documents.",
  "categoryId": "uuid",
  "priceFrom": 300,
  "currency": "USD",
  "deliveryDays": 14,
  "skillIds": ["uuid", "uuid"],
  "status": "DRAFT"
}
```

### Response `201`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "expertId": "uuid",
    "title": "RAG Chatbot Development",
    "description": "I will build a custom RAG chatbot for your business documents.",
    "categoryId": "uuid",
    "priceFrom": 300,
    "currency": "USD",
    "deliveryDays": 14,
    "status": "DRAFT",
    "createdAt": "2026-05-23T13:00:00Z"
  },
  "message": "Service created"
}
```

---

## 20.2 Publish Service

```http
POST /services/{serviceId}/publish
```

Roles: `EXPERT`

### Response `200`

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "status": "PUBLISHED"
  },
  "message": "Service published"
}
```

---

## 20.3 Get Services

```http
GET /services?categoryId=uuid&search=chatbot&page=1&pageSize=20
```

Roles: public or authenticated

### Response `200`

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "expertId": "uuid",
      "expertName": "Demo Expert",
      "expertRating": 4.8,
      "title": "RAG Chatbot Development",
      "description": "I will build a custom RAG chatbot.",
      "categoryName": "RAG",
      "priceFrom": 300,
      "currency": "USD",
      "deliveryDays": 14,
      "status": "PUBLISHED"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  },
  "message": "OK"
}
```

---

# 21. State Transition Rules

## 21.1 Job State

```text
DRAFT → OPEN
OPEN → IN_PROGRESS
OPEN → CANCELLED
IN_PROGRESS → COMPLETED
IN_PROGRESS → CANCELLED
```

Invalid examples:

```text
COMPLETED → OPEN
CANCELLED → OPEN
```

## 21.2 Proposal State

```text
SUBMITTED → SHORTLISTED
SUBMITTED → REJECTED
SUBMITTED → WITHDRAWN
SUBMITTED → ACCEPTED
SHORTLISTED → ACCEPTED
SHORTLISTED → REJECTED
SHORTLISTED → WITHDRAWN
```

After one proposal is accepted, sibling proposals should become `REJECTED`.

## 21.3 Project State

```text
PENDING_PAYMENT → ACTIVE
ACTIVE → IN_REVIEW
ACTIVE → DISPUTED
IN_REVIEW → ACTIVE
IN_REVIEW → COMPLETED
DISPUTED → ACTIVE
ACTIVE → COMPLETED
ACTIVE → CANCELLED
```

## 21.4 Milestone State

```text
CREATED → FUNDED
FUNDED → IN_PROGRESS
FUNDED → SUBMITTED
IN_PROGRESS → SUBMITTED
SUBMITTED → REVISION_REQUESTED
REVISION_REQUESTED → SUBMITTED
SUBMITTED → PAID
SUBMITTED → DISPUTED
DISPUTED → PAID
DISPUTED → REFUNDED
DISPUTED → REVISION_REQUESTED
```

## 21.5 Payment State

```text
PENDING → HELD
HELD → RELEASED
HELD → FROZEN
HELD → REFUNDED
FROZEN → RELEASED
FROZEN → REFUNDED
FROZEN → PARTIALLY_RELEASED
```

---

# 22. Error Code Catalog

| Code                        | Meaning                                          |
| --------------------------- | ------------------------------------------------ |
| `VALIDATION_ERROR`          | Request body or query is invalid                 |
| `UNAUTHORIZED`              | Missing or invalid token                         |
| `FORBIDDEN`                 | User does not have permission                    |
| `NOT_FOUND`                 | Resource does not exist                          |
| `DUPLICATE_EMAIL`           | Email already exists                             |
| `DUPLICATE_PROPOSAL`        | Expert already submitted proposal to this job    |
| `INVALID_ROLE`              | User role is not allowed                         |
| `INVALID_STATUS_TRANSITION` | Requested action is not valid for current status |
| `INSUFFICIENT_BALANCE`      | Wallet balance is not enough                     |
| `AI_PROVIDER_ERROR`         | AI provider failed                               |
| `PAYMENT_CONFLICT`          | Payment state is invalid for requested action    |
| `DISPUTE_ALREADY_OPEN`      | Milestone already has active dispute             |
| `PROJECT_NOT_COMPLETED`     | Review requires completed project                |

---

# 23. Example Error Responses

## 23.1 Insufficient Balance

```json
{
  "success": false,
  "error": {
    "code": "INSUFFICIENT_BALANCE",
    "message": "Client wallet balance is not enough to fund this milestone",
    "details": [
      {
        "field": "availableBalance",
        "message": "Available balance is 200 but required amount is 300"
      }
    ]
  }
}
```

## 23.2 Invalid Status Transition

```json
{
  "success": false,
  "error": {
    "code": "INVALID_STATUS_TRANSITION",
    "message": "Cannot approve milestone because it is not in SUBMITTED status",
    "details": [
      {
        "field": "milestone.status",
        "message": "Current status is FUNDED"
      }
    ]
  }
}
```

## 23.3 Duplicate Proposal

```json
{
  "success": false,
  "error": {
    "code": "DUPLICATE_PROPOSAL",
    "message": "Expert has already submitted a proposal for this job"
  }
}
```

---

# 24. MVP Endpoint Checklist

## Must-have

```http
POST /auth/register
POST /auth/login
GET /auth/me

PUT /clients/me/profile
PUT /experts/me/profile
POST /experts/me/skills
DELETE /experts/me/skills/{skillId}

GET /categories
GET /skills

POST /ai/job-assistant
POST /ai/job-assistant/{suggestionId}/accept

POST /jobs
GET /jobs
GET /jobs/{jobId}
PUT /jobs/{jobId}
POST /jobs/{jobId}/publish

POST /jobs/{jobId}/recommendations/generate
GET /jobs/{jobId}/recommendations

POST /jobs/{jobId}/proposals
GET /jobs/{jobId}/proposals
GET /proposals/{proposalId}
PUT /proposals/{proposalId}/shortlist
PUT /proposals/{proposalId}/reject
PUT /proposals/{proposalId}/withdraw
PUT /proposals/{proposalId}/accept

GET /projects
GET /projects/{projectId}
POST /projects/{projectId}/milestones
PUT /milestones/{milestoneId}/fund
POST /milestones/{milestoneId}/deliverables
GET /milestones/{milestoneId}/deliverables
PUT /milestones/{milestoneId}/request-revision
PUT /milestones/{milestoneId}/approve
POST /milestones/{milestoneId}/dispute

GET /wallet/me
POST /wallet/deposit-demo
GET /payments/history

POST /reviews
GET /users/{userId}/reviews

GET /admin/users
PUT /admin/users/{userId}/suspend
GET /admin/disputes
GET /admin/disputes/{disputeId}
POST /disputes/{disputeId}/evidence
PUT /admin/disputes/{disputeId}/resolve
```

## Should-have

```http
GET /conversations
GET /conversations/{conversationId}/messages
POST /conversations/{conversationId}/messages
PUT /conversations/{conversationId}/read

POST /ai/service-generator

POST /services
POST /services/{serviceId}/publish
GET /services
```

---

# 25. Notes for Backend Implementation

## 25.1 Transactional Operations

These operations must run inside database transactions:

1. Accept proposal and create project.
2. Fund milestone.
3. Approve milestone and release payment.
4. Open dispute and freeze payment.
5. Resolve dispute.
6. Cancel project if refunds are involved.

## 25.2 Authorization Checks

Every protected endpoint should check both:

1. User role.
2. Resource ownership.

Example:

```text
Client can approve milestone only if:
- user.role == CLIENT
- project.clientId == user.id
- milestone.projectId == project.id
```

## 25.3 AI Safety

AI suggestions must be treated as drafts.  
The system should never auto-publish AI-generated content without user confirmation.

## 25.4 Simulated Escrow

This MVP does not integrate a real payment gateway.  
Wallet, payment, and transaction records only simulate escrow behavior for academic/demo purposes.

## 25.5 File Upload

For MVP, deliverables can use URL fields:

```text
fileUrl
demoUrl
sourceCodeUrl
attachmentUrl
```

Direct file upload can be added later with object storage.

---

# 26. Suggested Folder Placement

Recommended location in backend repository:

```text
/docs/API_CONTRACT.md
```

or project root:

```text
API_CONTRACT.md
```
