using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Base;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.ReviewService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.ReviewResponse> CreateReviewAsync(Guid reviewerId, Request.CreateReviewRequest request)
    {
        var project = await _dbContext.Projects.FindAsync(request.ProjectId);
        if (project == null) throw new NotFoundException("Project not found.");

        if (project.Status != ProjectStatus.COMPLETED)
            throw new ValidationException("Reviews can only be given for completed projects.");

        if (project.ClientId != reviewerId && project.ExpertId != reviewerId)
            throw new UnauthorizedException("You are not authorized to review this project.");

        if (request.RevieweeId != project.ClientId && request.RevieweeId != project.ExpertId)
            throw new ValidationException("Reviewee must be a party in the project.");

        if (request.RevieweeId == reviewerId)
            throw new ValidationException("You cannot review yourself.");

        ValidateRating(request.Rating, "Rating");
        ValidateRating(request.CommunicationRating, "Communication rating");
        ValidateRating(request.QualityRating, "Quality rating");
        ValidateRating(request.DeadlineRating, "Deadline rating");
        ValidateRating(request.RequirementClarityRating, "Requirement clarity rating");

        var existingReview = await _dbContext.Reviews
            .FirstOrDefaultAsync(r => r.ProjectId == request.ProjectId && r.ReviewerId == reviewerId);
        if (existingReview != null)
            throw new ValidationException("You have already reviewed this project.");

        var review = new Review
        {
            ProjectId = request.ProjectId,
            ReviewerId = reviewerId,
            RevieweeId = request.RevieweeId,
            Rating = request.Rating,
            Comment = request.Comment,
            CommunicationRating = request.CommunicationRating,
            QualityRating = request.QualityRating,
            DeadlineRating = request.DeadlineRating,
            RequirementClarityRating = request.RequirementClarityRating
        };

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            _dbContext.Reviews.Add(review);
            await _dbContext.SaveChangesAsync();

            // Recalculate reviewee rating
            var profile = await _dbContext.ExpertProfiles.FirstOrDefaultAsync(p => p.UserId == request.RevieweeId);
            if (profile != null)
            {
                var reviews = await _dbContext.Reviews.Where(r => r.RevieweeId == request.RevieweeId).ToListAsync();
                profile.Rating = (decimal)reviews.Average(r => r.Rating);
                profile.TotalReviews = reviews.Count;
            }
            else
            {
                var clientProfile = await _dbContext.ClientProfiles.FirstOrDefaultAsync(p => p.UserId == request.RevieweeId);
                if (clientProfile != null)
                {
                    var reviews = await _dbContext.Reviews.Where(r => r.RevieweeId == request.RevieweeId).ToListAsync();
                    clientProfile.Rating = (decimal)reviews.Average(r => r.Rating);
                    clientProfile.TotalReviews = reviews.Count;
                }
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            var reviewer = await _dbContext.Users.FindAsync(reviewerId);
            return MapToResponse(review, reviewer?.FullName ?? "N/A");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Aivora.Services.Base.Response.PageResult<Response.ReviewResponse>> GetUserReviewsAsync(Guid userId, Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var query = _dbContext.Reviews
            .Include(r => r.Reviewer)
            .Where(r => r.RevieweeId == userId)
            .OrderByDescending(r => r.CreatedAt);

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        return new Aivora.Services.Base.Response.PageResult<Response.ReviewResponse>
        {
            Items = items.Select(r => MapToResponse(r, r.Reviewer?.FullName ?? "N/A")).ToList(),
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    private static void ValidateRating(int? rating, string fieldName)
    {
        if (rating.HasValue && (rating.Value < 1 || rating.Value > 5))
            throw new ValidationException($"{fieldName} must be between 1 and 5.");
    }

    private static Response.ReviewResponse MapToResponse(Review r, string reviewerName)
    {
        return new Response.ReviewResponse
        {
            Id = r.Id,
            ProjectId = r.ProjectId,
            ReviewerId = r.ReviewerId,
            ReviewerName = reviewerName,
            RevieweeId = r.RevieweeId,
            Rating = r.Rating,
            Comment = r.Comment,
            CommunicationRating = r.CommunicationRating,
            QualityRating = r.QualityRating,
            DeadlineRating = r.DeadlineRating,
            RequirementClarityRating = r.RequirementClarityRating,
            CreatedAt = r.CreatedAt
        };
    }
}
