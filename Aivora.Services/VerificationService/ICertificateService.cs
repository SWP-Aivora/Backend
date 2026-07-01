using Aivora.Repositories.Entities;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace Aivora.Services.VerificationService;

public interface ICertificateService
{
    Task<VerificationCertificate> UploadCertificateAsync(Guid expertId, IFormFile file, string certificateName, string issuingOrganization);
    Task<VerificationCertificate> VerifyCertificateAsync(Guid certificateId);
    Task<List<VerificationCertificate>> GetExpertCertificatesAsync(Guid expertId);
    Task<bool> DeleteCertificateAsync(Guid certificateId);
}