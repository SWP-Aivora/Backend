using System.Security.Claims;
using System.Text;
using Aivora.Repositories.Enums;
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
    public const string AdminOrParticipantPolicy = "AdminOrParticipantPolicy";
    public const string ClientOrExpertPolicy = "ClientOrExpertPolicy";

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

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Cookies["accessToken"];

                    // If cookie has the token, use it
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }

                    // SignalR support: allow overriding via query string
                    var accessTokenQuery = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessTokenQuery) && path.StartsWithSegments("/api/v1/chat"))
                    {
                        context.Token = accessTokenQuery;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ClientPolicy, policy => policy.RequireRole(nameof(UserRole.CLIENT)));
            options.AddPolicy(ExpertPolicy, policy => policy.RequireRole(nameof(UserRole.EXPERT)));
            options.AddPolicy(AdminPolicy, policy => policy.RequireRole(nameof(UserRole.ADMIN)));
            options.AddPolicy(WithdrawPolicy, policy => policy.RequireRole(nameof(UserRole.CLIENT), nameof(UserRole.EXPERT)));
            options.AddPolicy(AdminOrParticipantPolicy, policy => policy.RequireRole(nameof(UserRole.ADMIN), nameof(UserRole.CLIENT), nameof(UserRole.EXPERT)));
            options.AddPolicy(ClientOrExpertPolicy, policy => policy.RequireRole(nameof(UserRole.CLIENT), nameof(UserRole.EXPERT)));
        });
    }
}
