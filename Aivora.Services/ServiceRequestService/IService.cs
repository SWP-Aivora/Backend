using Aivora.Repositories.Enums;

namespace Aivora.Services.ServiceRequestService;

public interface IService
{
    Task<Response.ServiceRequestResponse> CreateRequestAsync(Guid clientId, Guid serviceId, Request.CreateServiceRequestRequest request);
    Task<List<Response.ServiceRequestResponse>> GetRequestsByServiceAsync(Guid expertId, Guid serviceId);
    Task<List<Response.ServiceRequestResponse>> GetMyRequestsForExpertAsync(Guid expertId, ServiceRequestStatus? status);
}
