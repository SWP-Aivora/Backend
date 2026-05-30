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

    public static void AddJwtServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = new JwtOptions();
        configuration.GetSection("JwtOptions").Bind(jwtOptions);
        var key = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);

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
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ClientPolicy, policy => policy.RequireRole("CLIENT"));
            options.AddPolicy(ExpertPolicy, policy => policy.RequireRole("EXPERT"));
            options.AddPolicy(AdminPolicy, policy => policy.RequireRole("ADMIN"));
        });
    }
}
