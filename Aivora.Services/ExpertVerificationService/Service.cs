using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ExpertVerificationService.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.ExpertVerificationService;

public class Service : IService
{
    // Below this, an "APPROVED" verdict from the AI is a guess, not a verdict — see SubmitEvidenceAsync.
    private const int MinAutoApproveConfidence = 70;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };

    private static readonly Dictionary<string, string> MimeTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf"
    };

    private readonly AivoraDbContext _dbContext;
    private readonly MediaService.IService _mediaService;
    private readonly IAIExpertVerificationProvider _aiProvider;
    private readonly NotificationService.IService _notificationService;
    private readonly ILogger<Service> _logger;

    public Service(
        AivoraDbContext dbContext,
        MediaService.IService mediaService,
        IAIExpertVerificationProvider aiProvider,
        NotificationService.IService notificationService,
        ILogger<Service> logger)
    {
        _dbContext = dbContext;
        _mediaService = mediaService;
        _aiProvider = aiProvider;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Response.ExpertVerificationResponse> SubmitEvidenceAsync(Guid expertUserId, Request.SubmitEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        var expert = await _dbContext.ExpertProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == expertUserId, cancellationToken);
        if (expert == null) throw new NotFoundException("Expert profile not found.");

        var expertSkill = await _dbContext.ExpertSkills
            .Include(s => s.Skill)
            .Include(s => s.Expert).ThenInclude(e => e.User)
            .FirstOrDefaultAsync(s => s.Id == request.ExpertSkillId && s.ExpertId == expert.Id, cancellationToken);
        if (expertSkill == null) throw new NotFoundException("Expert skill not found for the current expert.");

        if (request.File == null || request.File.Length == 0)
            throw new ValidationException("Evidence file is required.");

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new ValidationException($"File extension {extension} is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

        var uploadResult = extension == ".pdf"
            ? await _mediaService.UploadFileAsync(request.File, expertUserId, "certificates")
            : await _mediaService.UploadImageAsync(request.File, expertUserId, "certificates");

        byte[] fileBytes;
        await using (var stream = request.File.OpenReadStream())
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            fileBytes = memoryStream.ToArray();
        }

        var aiResult = await _aiProvider.AnalyzeEvidenceAsync(new AnalyzeEvidenceRequest
        {
            FileBytes = fileBytes,
            MimeType = MimeTypesByExtension[extension],
            ExpertFullName = expert.User.FullName,
            ClaimedSkillName = expertSkill.Skill.Name
        }, cancellationToken);

        // A low-confidence "APPROVED" is a guess, not a verdict — require a human to review it
        // rather than silently mark the skill verified.
        var outcome = aiResult.Outcome == ExpertVerificationStatus.APPROVED && aiResult.ConfidenceScore < MinAutoApproveConfidence
            ? ExpertVerificationStatus.NEEDS_REVIEW
            : aiResult.Outcome;

        var verification = new ExpertVerification
        {
            ExpertSkillId = expertSkill.Id,
            EvidenceFileUrl = uploadResult.Url,
            EvidencePublicId = uploadResult.PublicId,
            Status = outcome,
            AIConfidenceScore = aiResult.ConfidenceScore,
            AIReasoning = aiResult.Reasoning
        };

        _dbContext.ExpertVerifications.Add(verification);

        if (outcome == ExpertVerificationStatus.APPROVED)
        {
            expertSkill.IsVerified = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var canEscalate = await ComputeCanEscalateAsync(verification);

        _logger.LogInformation("Expert {ExpertId} submitted verification evidence for skill {SkillId}. Outcome: {Outcome}", expert.Id, expertSkill.Id, outcome);

        await SendVerificationNotificationAsync(expert.UserId, expertSkill.Skill.Name, outcome, aiResult.Reasoning);

        return MapToResponse(verification, expertSkill, canEscalate);
    }

    public async Task<Base.Response.PageResult<Response.ExpertVerificationResponse>> GetMyVerificationsAsync(Guid expertUserId, Guid? expertSkillId, Base.Request.PageRequest pageRequest)
    {
        var expert = await _dbContext.ExpertProfiles.FirstOrDefaultAsync(p => p.UserId == expertUserId);
        if (expert == null) throw new NotFoundException("Expert profile not found.");

        var query = _dbContext.ExpertVerifications
            .Include(v => v.ExpertSkill).ThenInclude(s => s.Skill)
            .Include(v => v.ExpertSkill).ThenInclude(s => s.Expert).ThenInclude(e => e.User)
            .Where(v => v.ExpertSkill.ExpertId == expert.Id);

        if (expertSkillId.HasValue)
        {
            query = query.Where(v => v.ExpertSkillId == expertSkillId.Value);
        }

        return await ToPageResultAsync(query, pageRequest, computeCanEscalate: true);
    }

    public async Task<Response.ExpertVerificationResponse> EscalateAsync(Guid expertUserId, Guid verificationId)
    {
        var expert = await _dbContext.ExpertProfiles.FirstOrDefaultAsync(p => p.UserId == expertUserId);
        if (expert == null) throw new NotFoundException("Expert profile not found.");

        var verification = await _dbContext.ExpertVerifications
            .Include(v => v.ExpertSkill).ThenInclude(s => s.Skill)
            .Include(v => v.ExpertSkill).ThenInclude(s => s.Expert).ThenInclude(e => e.User)
            .FirstOrDefaultAsync(v => v.Id == verificationId);

        if (verification == null || verification.ExpertSkill.ExpertId != expert.Id)
            throw new NotFoundException("Verification not found.");

        if (verification.Status != ExpertVerificationStatus.REJECTED && verification.Status != ExpertVerificationStatus.NEEDS_REVIEW)
            throw new ValidationException("This verification is not eligible for escalation.");

        var canEscalate = await ComputeCanEscalateAsync(verification);
        if (!canEscalate)
            throw new ValidationException("Escalation is only available after two consecutive non-approved submissions for this skill.");

        verification.Status = ExpertVerificationStatus.ESCALATED;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Expert {ExpertId} escalated verification {VerificationId}.", expert.Id, verification.Id);

        return MapToResponse(verification, verification.ExpertSkill, canEscalate: false);
    }

    public async Task<Base.Response.PageResult<Response.ExpertVerificationResponse>> GetAdminVerificationsAsync(Base.Request.PageRequest pageRequest, ExpertVerificationStatus? status, Guid? expertId, string? search = null)
    {
        var query = _dbContext.ExpertVerifications
            .Include(v => v.ExpertSkill).ThenInclude(s => s.Skill)
            .Include(v => v.ExpertSkill).ThenInclude(s => s.Expert).ThenInclude(e => e.User)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(v => v.Status == status.Value);
        }

        if (expertId.HasValue)
        {
            query = query.Where(v => v.ExpertSkill.ExpertId == expertId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.ToLower();
            query = query.Where(v =>
                v.ExpertSkill.Expert.User.FullName.ToLower().Contains(keyword) ||
                v.ExpertSkill.Skill.Name.ToLower().Contains(keyword));
        }

        return await ToPageResultAsync(query, pageRequest);
    }

    public async Task<Response.ExpertVerificationResponse> ReviewEscalatedVerificationAsync(Guid adminId, Guid verificationId, Request.ReviewVerificationRequest request)
    {
        var verification = await _dbContext.ExpertVerifications
            .Include(v => v.ExpertSkill).ThenInclude(s => s.Skill)
            .Include(v => v.ExpertSkill).ThenInclude(s => s.Expert).ThenInclude(e => e.User)
            .FirstOrDefaultAsync(v => v.Id == verificationId);

        if (verification == null) throw new NotFoundException("Verification not found.");
        if (verification.Status != ExpertVerificationStatus.ESCALATED && verification.Status != ExpertVerificationStatus.NEEDS_REVIEW)
            throw new ValidationException("Only escalated or needs-review verifications can be reviewed.");

        verification.AdminId = adminId;
        verification.ReviewedAt = DateTimeOffset.UtcNow;

        if (request.IsApproved)
        {
            verification.Status = ExpertVerificationStatus.APPROVED;
            verification.ExpertSkill.IsVerified = true;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RejectionReason))
                throw new ValidationException("Rejection reason is required when rejecting a verification.");

            verification.Status = ExpertVerificationStatus.REJECTED;
            verification.AdminDecisionReason = request.RejectionReason;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} reviewed escalated verification {VerificationId}. Approved: {IsApproved}", adminId, verificationId, request.IsApproved);

        await SendEscalationResolvedNotificationAsync(
            verification.ExpertSkill.Expert.UserId,
            verification.ExpertSkill.Skill.Name,
            request.IsApproved,
            request.RejectionReason);

        return MapToResponse(verification, verification.ExpertSkill, canEscalate: false);
    }

    private async Task<bool> ComputeCanEscalateAsync(ExpertVerification verification)
    {
        if (verification.Status == ExpertVerificationStatus.APPROVED)
            return false;

        var escalatableIds = await ComputeEscalatableVerificationIdsAsync(new[] { verification.ExpertSkillId });
        return escalatableIds.Contains(verification.Id);
    }

    /// <summary>
    /// For each given skill, returns the id of its latest ExpertVerification if that skill's
    /// two most recent submissions are both non-approved (the escalation-eligibility rule) —
    /// otherwise the skill contributes nothing to the result.
    /// </summary>
    private async Task<HashSet<Guid>> ComputeEscalatableVerificationIdsAsync(IEnumerable<Guid> expertSkillIds)
    {
        var escalatableIds = new HashSet<Guid>();

        foreach (var skillId in expertSkillIds.Distinct())
        {
            var lastTwo = await _dbContext.ExpertVerifications
                .Where(v => v.ExpertSkillId == skillId)
                .OrderByDescending(v => v.CreatedAt)
                .Take(2)
                .ToListAsync();

            if (lastTwo.Count == 2 && lastTwo.All(v => v.Status == ExpertVerificationStatus.REJECTED || v.Status == ExpertVerificationStatus.NEEDS_REVIEW))
            {
                escalatableIds.Add(lastTwo[0].Id);
            }
        }

        return escalatableIds;
    }

    private async Task<Base.Response.PageResult<Response.ExpertVerificationResponse>> ToPageResultAsync(IQueryable<ExpertVerification> query, Base.Request.PageRequest pageRequest, bool computeCanEscalate = false)
    {
        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        var escalatableIds = computeCanEscalate
            ? await ComputeEscalatableVerificationIdsAsync(items.Select(v => v.ExpertSkillId))
            : new HashSet<Guid>();

        return new Base.Response.PageResult<Response.ExpertVerificationResponse>
        {
            Items = items.Select(v => MapToResponse(v, v.ExpertSkill, canEscalate: escalatableIds.Contains(v.Id))).ToList(),
            TotalItems = totalItems,
            PageSize = pageRequest.PageSize,
            PageIndex = pageRequest.PageIndex
        };
    }

    private Task SendVerificationNotificationAsync(Guid userId, string skillName, ExpertVerificationStatus status, string? reasoning)
    {
        var (title, message) = status switch
        {
            ExpertVerificationStatus.APPROVED => ("Skill Verified", $"Your evidence for '{skillName}' was approved. This skill is now marked as verified."),
            ExpertVerificationStatus.REJECTED => ("Skill Verification Rejected", $"Your evidence for '{skillName}' was rejected. Reason: {reasoning}"),
            ExpertVerificationStatus.NEEDS_REVIEW => ("Skill Verification Needs Review", $"Your evidence for '{skillName}' needs a clearer resubmission. Reason: {reasoning}"),
            _ => ("Skill Verification Update", $"Your verification status for '{skillName}' has changed.")
        };

        return NotifyAsync(userId, title, message);
    }

    private Task SendEscalationResolvedNotificationAsync(Guid userId, string skillName, bool isApproved, string? reason)
    {
        var title = isApproved ? "Skill Verification Approved" : "Skill Verification Rejected";
        var message = isApproved
            ? $"An administrator reviewed your escalated evidence for '{skillName}' and approved it."
            : $"An administrator reviewed your escalated evidence for '{skillName}' and rejected it. Reason: {reason}";

        return NotifyAsync(userId, title, message);
    }

    private async Task NotifyAsync(Guid userId, string title, string message)
    {
        try
        {
            await _notificationService.SendNotificationAsync(userId, title, message, "VERIFICATION", "/expert/skills");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send verification notification to user {UserId}.", userId);
        }
    }

    private static Response.ExpertVerificationResponse MapToResponse(ExpertVerification verification, ExpertSkill expertSkill, bool canEscalate)
    {
        return new Response.ExpertVerificationResponse
        {
            Id = verification.Id,
            ExpertSkillId = verification.ExpertSkillId,
            SkillName = expertSkill?.Skill?.Name,
            ExpertId = expertSkill?.ExpertId ?? Guid.Empty,
            ExpertName = expertSkill?.Expert?.User?.FullName,
            EvidenceFileUrl = verification.EvidenceFileUrl,
            Status = verification.Status.ToString(),
            AIConfidenceScore = verification.AIConfidenceScore,
            AIReasoning = verification.AIReasoning,
            AdminId = verification.AdminId,
            AdminDecisionReason = verification.AdminDecisionReason,
            ReviewedAt = verification.ReviewedAt,
            CreatedAt = verification.CreatedAt,
            CanEscalate = canEscalate
        };
    }
}
