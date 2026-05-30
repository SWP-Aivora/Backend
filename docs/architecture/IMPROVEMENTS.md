# Architecture Improvement TODOs

List of identified architectural friction points and proposed deepening opportunities.

## 1. Recommendation Scoring Strategy
- **Status**: Pending
- **Problem**: The expert matching algorithm (math, weights, and scoring) is buried inside procedural DB-coupled service code (RecommendationService.cs). This makes it impossible to unit-test the algorithm in isolation.
- **Proposed Solution**: Apply the Strategy pattern. Extract scoring logic into an \IScoringStrategy\. The service gathers expert/job data, and the strategy performs the pure mathematical calculation.
- **Benefits**: Algorithm can be refined or swapped (e.g., AI-based vs. Rule-based) without touching data-access code. Enables 100% test coverage for matching logic.

## 2. Domain Lifecycle Transitions
- **Status**: Pending
- **Problem**: The domain model is \
anemic.\ Critical lifecycle transitions (e.g., publishing a job, starting a project) are performed by services directly setting status enums, leaking business rules across the service layer.
- **Proposed Solution**: Deepen entities by adding behavior methods: \job.Publish()\, \project.Complete()\. These methods validate invariants and set side-effects internally.
- **Benefits**: Business rules are centralized in the entities, making the \happy
path\ enforced by the type system. Reduces code duplication in services.
