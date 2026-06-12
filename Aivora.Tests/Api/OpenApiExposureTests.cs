using System.Net;
using FluentAssertions;

namespace Aivora.Tests.Api;

public class OpenApiExposureTests
{
    [Fact]
    public async Task Development_ExposesOpenApiDocument()
    {
        await using var factory = new AivoraApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("/api/v1/auth/login");
    }

    [Fact]
    public async Task Production_DoesNotExposeOpenApiDocument()
    {
        await using var factory = AivoraApiFactory.Production();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Production_MissingGeminiConfiguration_FailsFast()
    {
        using var factory = AivoraApiFactory.Production(new Dictionary<string, string?>
        {
            ["AIProvider:Provider"] = "Mock",
            ["AIProvider:ApiKey"] = "",
            ["AIProvider:EnableFallback"] = "true"
        });

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid AI provider configuration*");
    }
}
