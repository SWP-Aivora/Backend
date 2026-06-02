namespace Aivora.Services.HiringService;

public class Response
{
    public class HiringResultResponse
    {
        public Guid ProjectId { get; set; }
        public Guid JobId { get; set; }
        public Guid AcceptedProposalId { get; set; }
        public string Status { get; set; } = null!;
    }
}
