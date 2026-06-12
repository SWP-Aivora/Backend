using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Aivora.Tests.Api;

public class ApiAuthAndValidationTests
{
    [Fact]
    public async Task AdminEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = new AivoraApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/categories", new
        {
            Name = "AI Operations"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_WithClientRole_ReturnsForbidden()
    {
        await using var factory = new AivoraApiFactory();
        var client = factory.CreateClient();
        var accessToken = await RegisterAndGetAccessTokenAsync(client, "CLIENT");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.PostAsJsonAsync("/api/v1/categories", new
        {
            Name = "Client Created Category"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvalidModelBinding_ReturnsApiResponseValidationEnvelope()
    {
        await using var factory = new AivoraApiFactory();
        var client = factory.CreateClient();
        var accessToken = await RegisterAndGetAccessTokenAsync(client, "CLIENT");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            "{\"categoryId\":\"not-a-guid\",\"budgetMin\":\"not-a-number\"}",
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("message").GetString().Should().Be("Validation failed.");
        document.RootElement.GetProperty("errors").GetProperty("code").GetString().Should().Be("validation_error");
    }

    private static async Task<string> RegisterAndGetAccessTokenAsync(HttpClient client, string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = $"user-{Guid.NewGuid():N}@example.com",
            Password = "Password123!",
            Role = role,
            FullName = $"{role} Test User"
        });

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement
            .GetProperty("data")
            .GetProperty("accessToken")
            .GetString()!;
    }
}
