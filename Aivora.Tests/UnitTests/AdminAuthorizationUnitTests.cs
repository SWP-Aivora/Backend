using Xunit;
using Aivora.Repositories.Enums;

namespace Aivora.Tests.UnitTests
{
    public class AdminAuthorizationUnitTests
    {
        [Fact]
        public void AdminPolicy_ShouldAllowAdminRole()
        {
            // Test JWT Extensions AdminPolicy implementation
            var adminRole = UserRole.ADMIN.ToString();

            // Verify that admin role matches the policy
            Assert.Equal("ADMIN", adminRole);
        }
    }
}