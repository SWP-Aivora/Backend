using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authorization;
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
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Nhập Token theo định dạng: Bearer {token}"
                };

                document.Security ??= new List<OpenApiSecurityRequirement>();
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document, "/components/securitySchemes/Bearer")] = new List<string>()
                });

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var allowAnonymous = metadata.OfType<IAllowAnonymous>().Any();
                var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();

                if (allowAnonymous || !requiresAuthorization)
                {
                    operation.Security = new List<OpenApiSecurityRequirement>();
                }

                return Task.CompletedTask;
            });
        });
    }

    public static void UseOpenApiUI(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        app.MapOpenApi("/openapi/{documentName}.json");
        app.MapScalarApiReference("/scalar", options =>
        {
            options.OpenApiRoutePattern = "/openapi/{documentName}.json";
            options.DynamicBaseServerUrl = true;
        });
    }
}
