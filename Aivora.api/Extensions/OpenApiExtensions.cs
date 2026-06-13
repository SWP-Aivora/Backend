using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Aivora.api.Extensions;

public static class OpenApiExtensions
{
    public static void AddOpenApiServices(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                // Add API info if missing
                document.Info ??= new OpenApiInfo
                {
                    Title = "Aivora API",
                    Version = "v1",
                    Description = "Aivora Backend API — Marketplace kết nối Client với Expert"
                };

                // Add JWT Bearer security scheme
                if (document.Components is null)
                    document.Components = new OpenApiComponents();
                document.Components.SecuritySchemes!["Bearer"] = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Nhập Token theo định dạng: Bearer {token}"
                };

                // Apply security requirement globally
                if (document.Security is null)
                    document.Security = new List<OpenApiSecurityRequirement>();
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document, "/components/securitySchemes/Bearer")] = new List<string>()
                });

                return Task.CompletedTask;
            });
        });
    }

    public static void UseOpenApiUI(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(); // Accessible at /scalar/v1
    }
}
