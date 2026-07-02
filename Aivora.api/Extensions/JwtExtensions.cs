using System.Security.Claims;
using System.Text;
using Aivora.Services.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Aivora.api.Extensions;

public static class JwtExtensions
{
    public const string ClientPolicy = "ClientPolicy";
    public const string ExpertPolicy = "ExpertPolicy";
    public const string AdminPolicy = "AdminPolicy";
    public const string WithdrawPolicy = "WithdrawPolicy";

    public static void AddJwtServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = new JwtOptions();
        configuration.GetSection("JwtSettings").Bind(jwtOptions);
        var key = Encoding.UTF8.GetBytes(jwtOptions.Secret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role
            };

            // Browsers' native WebSocket API cannot set Authorization headers,
            // so the SignalR client sends the JWT via ?access_token= instead.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    // Only extract from query for SignalR hub paths
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/api/v1/chat"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ClientPolicy, policy => policy.RequireRole("CLIENT"));
            options.AddPolicy(ExpertPolicy, policy => policy.RequireRole("EXPERT"));
            options.AddPolicy(AdminPolicy, policy => policy.RequireRole("ADMIN"));
            options.AddPolicy(WithdrawPolicy, policy => policy.RequireRole("CLIENT", "EXPERT"));
        });
    }
}
