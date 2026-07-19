using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;

namespace Aivora.Services.HiringService;

public static class ProjectFactory
{
    public static Project CreateProjectWithMilestones(
        Guid clientId,
        Guid expertId,
        string title,
        string? description,
        decimal? budget,
        string currency,
        Guid? jobId,
        Guid? acceptedProposalId,
        Guid? serviceRequestId,
        IEnumerable<(string Title, string? Description, decimal Amount, int OrderIndex)> milestones)
    {
        return new Project
        {
            JobId = jobId,
            AcceptedProposalId = acceptedProposalId,
            ServiceRequestId = serviceRequestId,
            ClientId = clientId,
            ExpertId = expertId,
            Title = title,
            Description = description,
            TotalBudget = budget,
            Currency = currency,
            Status = ProjectStatus.PENDING_PAYMENT,
            Milestones = milestones.Select(m => new Milestone
            {
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                OrderIndex = m.OrderIndex,
                Status = MilestoneStatus.CREATED,
                Currency = currency,
                Steps = new List<MilestoneStep>
                {
                    new MilestoneStep
                    {
                        Title = "Created",
                        Description = "Milestone created",
                        OrderIndex = 0,
                        Status = MilestoneStepStatus.COMPLETED,
                        CompletedAt = DateTimeOffset.UtcNow,
                        CompletedByUserId = clientId
                    }
                }
            }).ToList()
        };
    }
}
