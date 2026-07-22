namespace Aivora.Services.ServiceOfferService;

public interface IService
{
    Task<Response.ServiceOfferResponse> CreateOfferAsync(Guid expertId, Guid serviceRequestId, Request.CreateServiceOfferRequest request);
    Task<Response.AcceptServiceOfferResultResponse> AcceptOfferAsync(Guid clientId, Guid serviceOfferId);
    Task<Response.ServiceOfferResponse> GetOfferForServiceRequestAsync(Guid clientId, Guid serviceRequestId);
}
