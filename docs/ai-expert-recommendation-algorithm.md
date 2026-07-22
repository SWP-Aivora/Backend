# Thuật toán AI Expert Recommendation

Tổng hợp cơ chế "recommend expert khi tạo job" — hybrid: rule-based scorer lọc candidate, Gemini re-rank + sinh lý do match. Không AI thì rơi về Mock (deterministic = đúng thứ tự scorer).

## Luồng tổng quan

```
Client bấm "Generate Recommendations"
        │
        ▼
POST /jobs/{id}/recommendations/generate
        │
        ▼
1. Query pool tối đa 50 expert active, match skill    (Service.cs)
        │
        ▼
2. Chấm điểm rule-based cho từng expert (0..100)       (RecommendationScorer.cs)
        │
        ▼
3. Lấy top-12 theo TotalScore                          (Service.cs)
        │
        ▼
4. Gửi Gemini re-rank top-12 → chọn/sắp tối đa 5        (GeminiExpertRecommendationProvider.cs)
   (không có API key / lỗi → Mock, giữ nguyên thứ tự scorer)
        │
        ▼
5. Ghi đè Explanation + AiRank (KHÔNG đụng TotalScore)  (Service.cs)
        │
        ▼
6. Lưu RecommendationResult, GET sort theo AiRank       (Service.cs)
```

Nguyên tắc cốt lõi: **scorer giữ toàn bộ con số** (TotalScore, SkillScore, RatingScore, BudgetScore, AvailabilityScore, CompletionScore) — AI chỉ quyết **thứ tự** (`AiRank`) và **lý do** (`Explanation`). Vì vậy breakdown điểm luôn coherent (tổng = weighted sum các phần), dù bật hay tắt AI.

---

## 1. Endpoint

`Aivora.api/Controllers/JobController.cs:124-140`

```csharp
[HttpPost("{id}/recommendations/generate")]
[Authorize(Policy = JwtExtensions.ClientPolicy)]
public async Task<IActionResult> GenerateRecommendations(Guid id)
{
    var clientId = this.GetUserId();
    var result = await _recommendationService.GenerateRecommendationsAsync(clientId, id);
    return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.SuccessResponse(result, "Expert recommendations generated", HttpContext.TraceIdentifier));
}

[HttpGet("{id}/recommendations")]
[Authorize(Policy = JwtExtensions.ClientPolicy)]
public async Task<IActionResult> GetRecommendations(Guid id)
{
    var clientId = this.GetUserId();
    var result = await _recommendationService.GetRecommendationsAsync(clientId, id);
    return Ok(ApiResponseFactory.SuccessResponse(result, "Expert recommendations retrieved", HttpContext.TraceIdentifier));
}
```

Trigger thủ công (client bấm nút), **không** tự động lúc tạo job.

---

## 2. Query candidate pool (tối đa 50)

`Aivora.Services/RecommendationService/Service.cs:40-64`

```csharp
var activeExpertsQuery = _dbContext.ExpertProfiles
    .Where(e => e.User.Role == UserRole.EXPERT && e.User.Status == UserStatus.ACTIVE);

if (requiredSkills.Count > 0)
{
    var requiredSkillIds = requiredSkills.Select(rs => rs.SkillId).ToList();
    activeExpertsQuery = activeExpertsQuery
        .Where(e => e.ExpertSkills.Any(es => requiredSkillIds.Contains(es.SkillId)))
        .OrderByDescending(e => e.ExpertSkills.Count(es => requiredSkillIds.Contains(es.SkillId)))
        .ThenByDescending(e => e.Rating);
}
else
{
    activeExpertsQuery = activeExpertsQuery.OrderByDescending(e => e.Rating);
}

// Limit the candidate pool to the top 50 experts at database level to prevent memory issues
activeExpertsQuery = activeExpertsQuery.Take(50);
```

Kèm gom `disputeCounts` (`:66-70`) và `milestoneStats` — số milestone overdue trên project ACTIVE/DISPUTED (`:73-88`) để phạt điểm.

---

## 3. Công thức chấm điểm rule-based (0..100)

`Aivora.Services/RecommendationService/RecommendationScorer.cs:27-83`

```csharp
var skillScore = CalculateSkillScore(requiredSkills, expert, out var matchedSkillNames);
var budgetScore = CalculateBudgetScore(job, expert);
var ratingScore = Math.Round(expert.Rating * 20, 2);
var availabilityScore = expert.AvailabilityStatus == AvailabilityStatus.AVAILABLE ? 100m : 50m;
var completionScore = expert.SuccessRate > 0 ? expert.SuccessRate : 80m;

var totalScore = Math.Round(
    (skillScore * 0.40m)
    + (budgetScore * 0.20m)
    + (ratingScore * 0.20m)
    + (availabilityScore * 0.10m)
    + (completionScore * 0.10m),
    2);

// Phạt dispute: dispute rate * 1.5, cap 50%
var disputeRate = expert.CompletedProjects >= 3 && expert.CompletedProjects > 0
    ? (decimal)disputeCount / expert.CompletedProjects
    : 0m;
var penalty = Math.Min(disputeRate * options.DisputePenaltyFactor, options.MaxDisputePenalty);

// Phạt overdue milestone: overdue rate * 0.3, cap 30%
var overdueRate = totalMilestoneCount > 0
    ? (decimal)overdueMilestoneCount / totalMilestoneCount
    : 0m;
var overduePenalty = Math.Min(overdueRate * options.OverduePenaltyFactor, options.MaxOverduePenalty);

totalScore = Math.Round(totalScore * (1 - penalty) * (1 - overduePenalty), 2);
```

**Trọng số:** Skill 40% · Budget 20% · Rating 20% · Availability 10% · Completion 10%, nhân phạt dispute/overdue.

Skill score = trung bình trọng số theo level (`CalculateSkillScore`, `:85-110`): BEGINNER 0.5 · INTERMEDIATE 0.75 · ADVANCED 0.9 · EXPERT 1.0.

Budget score = 100 nếu chi phí ước tính (hourly rate, hoặc rate×timeline×6 cho FIXED) nằm trong `[BudgetMin, BudgetMax]` của job, giảm dần nếu vượt (`CalculateBudgetScore`, `:124-145`).

---

## 4. Lấy top-12, build context gửi AI

`Aivora.Services/RecommendationService/Service.cs:96-136`

```csharp
var scored = experts
    .Select(expert => { /* BuildRecommendation → RecommendationScorer.Score */ })
    .OrderByDescending(x => x.Result.TotalScore)
    .Take(CandidatePoolSize)   // = 12, const tại Service.cs:15
    .ToList();

var context = new ExpertRecommendationContext
{
    JobTitle = job.Title,
    JobDescription = job.FinalDescription ?? job.OriginalDescription,
    RequiredSkills = requiredSkills.Select(rs => rs.SkillName).ToList(),
    BudgetType = job.BudgetType.ToString(),
    BudgetMin = job.BudgetMin,
    BudgetMax = job.BudgetMax,
    Candidates = scored.Select(x => new CandidateExpert
    {
        ExpertId = x.Expert.UserId,
        Skills = x.Expert.ExpertSkills.Select(es => es.Skill.Name).ToList(),
        Rating = x.Expert.Rating,
        HourlyRate = x.Expert.HourlyRate,
        AvailabilityStatus = x.Expert.AvailabilityStatus.ToString(),
        SuccessRate = x.Expert.SuccessRate,
        CompletedProjects = x.Expert.CompletedProjects,
        DisputeCount = x.DisputeCount,
        OverdueRate = x.Result.OverdueRate,
        ScorerTotalScore = x.Result.TotalScore,
        ScorerExplanation = x.Result.Explanation ?? string.Empty
    }).ToList()
};
```

DTO định nghĩa tại `Aivora.Services/RecommendationService/ExpertRecommendationContext.cs`.

---

## 5. Prompt gửi Gemini

`Aivora.Services/RecommendationService/Prompting/ExpertRecommendationPromptBuilder.cs`

```csharp
public string Build(ExpertRecommendationContext context)
{
    return $$"""
        You are an AI Expert Recommendation engine for an AI-services freelance marketplace.
        From the candidate experts below, select and rank the best matches for this job.
        Each candidate already has a scorerTotalScore (0-100) computed by a rule-based system
        (skill match, budget fit, rating, availability, completion rate) — use it as a strong signal,
        but you may reorder candidates when the job title/description reveals a better fit.
        Job and candidates:
        {{JsonSerializer.Serialize(context)}}

        Return ONLY one JSON object with this schema:
        {
          "ranked": [
            { "expertId": "guid of a candidate above", "reasoning": "short reason this expert fits the job" }
          ]
        }
        Rank best first. Include at most 5 experts. Use only expertId values from the candidate list above.
        """;
}
```

Model: `gemini-2.5-flash` (default, config `AIProvider:Model`). **`temperature: 0`** cho lần gọi này — bắt buộc để ranking ổn định giữa các lần generate (listwise LLM re-rank ở temp cao dễ đảo thứ hạng dù input không đổi).

---

## 6. Gọi Gemini + fallback

`Aivora.Services/RecommendationService/Providers/GeminiExpertRecommendationProvider.cs`

```csharp
public Task<ExpertRecommendationDraft> RankAsync(ExpertRecommendationContext context, CancellationToken cancellationToken = default)
{
    return ExecuteAsync(
        buildPrompt: () => _promptBuilder.Build(context),
        parse: providerText => _parser.Parse(providerText, context, _logger),
        mockFallback: ct => _fallbackProvider.RankAsync(context, ct),
        logNoun: "expert recommendation",
        errorNoun: "expert recommendation",
        cancellationToken,
        temperature: 0);
}
```

`ExecuteAsync` dùng chung với AI Job Assistant, tại `Aivora.Services/AIJobAssistantService/Providers/GeminiProviderBase.cs:20-54`:

```csharp
if (!_client.HasApiKey && _options.EnableFallback)
{
    // Thiếu ApiKey → dùng Mock ngay, không gọi Gemini
    return await mockFallback(cancellationToken);
}
try
{
    var providerText = await _client.GenerateAsync(buildPrompt(), Array.Empty<(string, byte[])>(), cancellationToken, temperature);
    return parse(providerText);
}
catch (Exception ex) when (_options.EnableFallback)
{
    // Gemini lỗi/timeout → fallback Mock, log warning
    return await mockFallback(cancellationToken);
}
```

HTTP call thật tại `GeminiProviderClient.cs:27-113` — `POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`, header `x-goog-api-key`.

**Mock provider** (`Aivora.Services/RecommendationService/Providers/MockExpertRecommendationProvider.cs`) — deterministic, dùng khi không có API key:

```csharp
public Task<ExpertRecommendationDraft> RankAsync(ExpertRecommendationContext context, CancellationToken cancellationToken = default)
{
    return Task.FromResult(ExpertRecommendationParser.BuildScorerOrderDraft(context));
}
```

`BuildScorerOrderDraft` giữ nguyên thứ tự scorer, cap 5, `Reasoning = c.ScorerExplanation` (câu giải thích rule-based gốc) — offline app hành xử y hệt bản rule-based thuần trước đây.

---

## 7. Parse response Gemini

`Aivora.Services/RecommendationService/Parsing/ExpertRecommendationParser.cs:12-63`

- Lọc `expertId` không nằm trong 12 candidate gửi lên (chống hallucination).
- Dedupe (`seen.Add`).
- Cap tối đa 5 (`MaxRanked`).
- Reasoning > 2000 ký tự → cắt (khớp `HasMaxLength(2000)` của cột `Explanation`).
- JSON hỏng / thiếu `ranked` / rỗng sau lọc → fallback `BuildScorerOrderDraft` (= thứ tự scorer).

---

## 8. Ghi kết quả — scorer giữ số, AI quyết thứ tự + lý do

`Aivora.Services/RecommendationService/Service.cs:138-156`

```csharp
var draft = await _recommendationProvider.RankAsync(context, CancellationToken.None);

var resultsByExpertId = scored.ToDictionary(x => x.Expert.UserId, x => x.Result);
var recommendations = new List<RecommendationResult>();
var rank = 1;
foreach (var ranked in draft.Ranked)
{
    if (!resultsByExpertId.TryGetValue(ranked.ExpertId, out var result))
        continue;

    result.Explanation = ranked.Reasoning;   // AI ghi đè lý do
    result.AiRank = rank++;                  // AI quyết thứ tự
    recommendations.Add(result);
}
// TotalScore/SkillScore/RatingScore/... KHÔNG bị đụng — vẫn nguyên từ scorer
```

Entity `RecommendationResult` (`Aivora.Repositories/Entities/RecommendationResult.cs`) có cột `AiRank` (int, thêm qua migration `20260722002529_AddAiRankToRecommendationResult`).

`GetRecommendationsAsync` sort theo `AiRank` (không phải `TotalScore`) — `Service.cs:169`:

```csharp
.OrderBy(r => r.AiRank)
```

---

## 9. DI — chọn Gemini hay Mock

`Aivora.api/Extensions/ServiceCollectionExtensions.cs:210-221`

```csharp
services.AddScoped<IExpertRecommendationProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
    return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(options.ApiKey)
        ? sp.GetRequiredService<GeminiExpertRecommendationProvider>()
        : sp.GetRequiredService<MockExpertRecommendationProvider>();
});
```

Điều khiển bằng config `AIProvider:Provider` + `AIProvider:ApiKey` (env `AIProvider__Provider`, `AIProvider__ApiKey`) — dùng chung với AI Job Assistant, AI Service Generator.

---

## File map

| File | Vai trò |
|---|---|
| `Aivora.api/Controllers/JobController.cs:124-140` | 2 endpoint generate/get |
| `Aivora.Services/RecommendationService/Service.cs` | Orchestrator: query pool → scorer → top-12 → gọi AI → ghi AiRank/Explanation |
| `Aivora.Services/RecommendationService/RecommendationScorer.cs` | Công thức rule-based 0..100 |
| `Aivora.Services/RecommendationService/IExpertRecommendationProvider.cs` | Interface Strategy |
| `Aivora.Services/RecommendationService/ExpertRecommendationContext.cs` | DTO input (job + candidates) gửi AI |
| `Aivora.Services/RecommendationService/ExpertRecommendationDraft.cs` | DTO output AI trả về |
| `Aivora.Services/RecommendationService/Prompting/ExpertRecommendationPromptBuilder.cs` | Prompt template |
| `Aivora.Services/RecommendationService/Parsing/ExpertRecommendationParser.cs` | Parse + validate response Gemini, fallback |
| `Aivora.Services/RecommendationService/Providers/GeminiExpertRecommendationProvider.cs` | Gọi Gemini thật (temp=0) |
| `Aivora.Services/RecommendationService/Providers/MockExpertRecommendationProvider.cs` | Fallback deterministic |
| `Aivora.Services/AIJobAssistantService/Providers/GeminiProviderClient.cs` | HTTP client dùng chung mọi AI feature |
| `Aivora.Services/AIJobAssistantService/Providers/GeminiProviderBase.cs` | `ExecuteAsync` — logic fallback dùng chung |
| `Aivora.Repositories/Entities/RecommendationResult.cs` | Entity lưu kết quả (có `AiRank`) |
| `Aivora.api/Extensions/ServiceCollectionExtensions.cs:210-221` | DI factory Gemini/Mock |
| `Aivora.Tests/Services/ExpertRecommendationParserTests.cs` | Unit test parser (filter/dedupe/cap/truncate) |
| `Aivora.Tests/Services/RecommendationServiceTests.cs` | Unit test scorer + flow (dùng Mock provider) |
