namespace Aivora.Services.ServiceCatalogService;

public interface IService
{
    Task<Response.ServiceResponse> CreateServiceAsync(Guid expertId, Request.CreateServiceRequest request);
    Task<Response.ServiceResponse> UpdateServiceAsync(Guid expertId, Guid serviceId, Request.UpdateServiceRequest request);
    Task<Response.ServiceResponse> PublishServiceAsync(Guid expertId, Guid serviceId);
    Task<Response.ServiceResponse> UnpublishServiceAsync(Guid expertId, Guid serviceId);
    Task<List<Response.ServiceResponse>> GetMyServicesAsync(Guid expertId);
    Task<Response.ServiceResponse> GetServiceByIdAsync(Guid serviceId, Guid? viewerId);
}
