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
                var securityScheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Nhập Token của bạn theo định dạng: Bearer {token}"
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes.Add("Bearer", securityScheme);

                document.Security ??= new List<OpenApiSecurityRequirement>();
                var schemeRef = new OpenApiSecuritySchemeReference("Bearer", document, "/components/securitySchemes/Bearer");
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [schemeRef] = new List<string>()
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
