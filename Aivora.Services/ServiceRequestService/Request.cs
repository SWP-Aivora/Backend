namespace Aivora.Services.ServiceRequestService;

public class Request
{
    public class CreateServiceRequestRequest
    {
        public Guid PackageId { get; set; }
        public string? Note { get; set; }
    }
}
