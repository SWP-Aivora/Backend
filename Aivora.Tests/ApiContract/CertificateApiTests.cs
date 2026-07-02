using System.Net;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.JwtService;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aivora.Tests.ApiContract;

public class CertificateApiTests : IClassFixture<ApiContractTestFactory>
{
    private readonly ApiContractTestFactory _factory;

    public CertificateApiTests(ApiContractTestFactory factory)
    {
        _factory = factory;
        _factory.SeedDatabase();
    }

    private ApiContractClient CreateSecondExpertClient()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Aivora.Repositories.Data.AivoraDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();

        var user = new User
        {
            Email = $"expert-{Guid.NewGuid()}@aivora.com",
            PasswordHash = "hash",
            FullName = "Second Expert",
            Role = UserRole.EXPERT,
            Status = UserStatus.ACTIVE
        };
        db.Users.Add(user);
        db.ExpertProfiles.Add(new ExpertProfile { UserId = user.Id, Title = "Second Expert" });
        db.SaveChanges();

        var client = new ApiContractClient(_factory.CreateClient());
        client.SetToken(jwt.GenerateAccessToken(user, UserRole.EXPERT.ToString()));
        return client;
    }

    [Fact]
    public async Task PostCertificates_AsExpert_UploadsAndReturnsCertificate()
    {
        // Arrange
        var client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.EXPERT));

        // Act
        var (response, body) = await client.PostMultipartAsync(
            "/api/v1/verification/certificates?certificateName=AWS%20Certified&issuingOrganization=Amazon",
            "fake certificate contents",
            "cert.pdf");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Value.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = body.Value.GetProperty("data");
        data.GetProperty("certificateName").GetString().Should().Be("AWS Certified");
        data.GetProperty("issuingOrganization").GetString().Should().Be("Amazon");
        data.GetProperty("certificateUrl").GetString().Should().Contain("certificates/cert.pdf");
        data.GetProperty("isVerified").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetCertificates_AsExpert_ReturnsOnlyOwnCertificates()
    {
        // Arrange
        var client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.EXPERT));
        await client.PostMultipartAsync(
            "/api/v1/verification/certificates?certificateName=AWS%20Certified&issuingOrganization=Amazon",
            "fake certificate contents",
            "cert.pdf");

        // Act
        var (response, body) = await client.GetAsync("/api/v1/verification/certificates");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = body!.Value.GetProperty("data");
        data.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        data[0].GetProperty("certificateName").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeleteCertificate_AsOwningExpert_RemovesCertificate()
    {
        // Arrange
        var client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.EXPERT));
        var (_, uploadBody) = await client.PostMultipartAsync(
            "/api/v1/verification/certificates?certificateName=AWS%20Certified&issuingOrganization=Amazon",
            "fake certificate contents",
            "cert-to-delete.pdf");
        var certificateId = uploadBody!.Value.GetProperty("data").GetProperty("id").GetString();

        // Act
        var (response, body) = await client.DeleteAsync($"/api/v1/verification/certificates/{certificateId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Value.GetProperty("success").GetBoolean().Should().BeTrue();

        var (_, listBody) = await client.GetAsync("/api/v1/verification/certificates");
        var remaining = listBody!.Value.GetProperty("data");
        foreach (var cert in remaining.EnumerateArray())
        {
            cert.GetProperty("id").GetString().Should().NotBe(certificateId);
        }
    }

    [Fact]
    public async Task DeleteCertificate_AsNonOwningExpert_IsRejected()
    {
        // Arrange
        var owner = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.EXPERT));
        var (_, uploadBody) = await owner.PostMultipartAsync(
            "/api/v1/verification/certificates?certificateName=AWS%20Certified&issuingOrganization=Amazon",
            "fake certificate contents",
            "cert-owned-by-someone-else.pdf");
        var certificateId = uploadBody!.Value.GetProperty("data").GetProperty("id").GetString();

        var intruder = CreateSecondExpertClient();

        // Act
        var (response, _) = await intruder.DeleteAsync($"/api/v1/verification/certificates/{certificateId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, listBody) = await owner.GetAsync("/api/v1/verification/certificates");
        var remaining = listBody!.Value.GetProperty("data");
        remaining.EnumerateArray().Should().Contain(c => c.GetProperty("id").GetString() == certificateId);
    }
}
