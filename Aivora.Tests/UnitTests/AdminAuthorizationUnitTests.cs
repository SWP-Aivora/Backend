using System.Security.Claims;
using Aivora.api.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aivora.Tests.UnitTests
{
    /// <summary>
    /// Exercises the real ASP.NET Core authorization pipeline against the same
    /// AdminPolicy definition registered in JwtExtensions.AddJwtServices
    /// (RequireRole("ADMIN")), without needing a full JWT/HTTP host.
    /// </summary>
    public class AdminAuthorizationUnitTests
    {
        private static IAuthorizationService BuildAuthorizationService()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorizationCore(options =>
            {
                options.AddPolicy(JwtExtensions.AdminPolicy, policy => policy.RequireRole("ADMIN"));
            });
            return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        }

        private static ClaimsPrincipal BuildUser(Aivora.Repositories.Enums.UserRole? role)
        {
            var identity = role is null
                ? new ClaimsIdentity()
                : new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role.ToString()!) }, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public async Task AdminPolicy_AdminRole_Succeeds()
        {
            var authService = BuildAuthorizationService();
            var user = BuildUser(Aivora.Repositories.Enums.UserRole.ADMIN);

            var result = await authService.AuthorizeAsync(user, JwtExtensions.AdminPolicy);

            result.Succeeded.Should().BeTrue();
        }

        [Fact]
        public async Task AdminPolicy_NonAdminRole_Fails()
        {
            var authService = BuildAuthorizationService();
            var user = BuildUser(Aivora.Repositories.Enums.UserRole.CLIENT);

            var result = await authService.AuthorizeAsync(user, JwtExtensions.AdminPolicy);

            result.Succeeded.Should().BeFalse();
        }

        [Fact]
        public async Task AdminPolicy_AnonymousUser_Fails()
        {
            var authService = BuildAuthorizationService();
            var user = BuildUser(null);

            var result = await authService.AuthorizeAsync(user, JwtExtensions.AdminPolicy);

            result.Succeeded.Should().BeFalse();
        }
    }
}
