using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.Exceptions;
using Aivora.Services.MediaService;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.VerificationService;

public class CertificateService : ICertificateService
{
    private readonly AivoraDbContext _dbContext;
    private readonly IService _mediaService;

    public CertificateService(AivoraDbContext dbContext, IService mediaService)
    {
        _dbContext = dbContext;
        _mediaService = mediaService;
    }

    public async Task<VerificationCertificate> UploadCertificateAsync(Guid expertId, IFormFile file, string certificateName, string issuingOrganization)
    {
        var expert = await _dbContext.ExpertProfiles.FindAsync(expertId);
        if (expert == null)
            throw new NotFoundException("Expert not found");

        var upload = await _mediaService.UploadFileAsync(file, "certificates");

        var certificate = new VerificationCertificate
        {
            ExpertId = expertId,
            CertificateName = certificateName,
            IssuingOrganization = issuingOrganization,
            CertificateUrl = upload.Url,
            IssueDate = DateTime.UtcNow,
            Score = 0,
            IsVerified = false
        };

        _dbContext.VerificationCertificates.Add(certificate);
        await _dbContext.SaveChangesAsync();

        return certificate;
    }

    public async Task<VerificationCertificate> VerifyCertificateAsync(Guid certificateId)
    {
        var certificate = await _dbContext.VerificationCertificates.FindAsync(certificateId);
        if (certificate == null)
            throw new NotFoundException("Certificate not found");

        certificate.IsVerified = true;
        await _dbContext.SaveChangesAsync();

        return certificate;
    }

    public Task<List<VerificationCertificate>> GetExpertCertificatesAsync(Guid expertId)
    {
        return _dbContext.VerificationCertificates
            .Where(c => c.ExpertId == expertId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteCertificateAsync(Guid certificateId)
    {
        var certificate = await _dbContext.VerificationCertificates.FindAsync(certificateId);
        if (certificate == null)
            return false;

        _dbContext.VerificationCertificates.Remove(certificate);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<Guid> GetExpertProfileIdAsync(Guid userId)
    {
        var expert = await _dbContext.ExpertProfiles.FirstOrDefaultAsync(e => e.UserId == userId);
        if (expert == null)
            throw new NotFoundException("Expert profile not found");

        return expert.Id;
    }
}
