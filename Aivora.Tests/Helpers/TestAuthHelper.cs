using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aivora.Tests.Helpers
{
    public class TestAuthHelper
    {
        private readonly HttpClient _client;

        public TestAuthHelper(HttpClient client)
        {
            _client = client;
        }

        public async Task<string> GetAdminTokenAsync()
        {
            // For integration tests, we'll use a placeholder token
            // In a real implementation, this would login with test admin credentials
            return "admin-jwt-token";
        }

        public async Task<string> GetClientTokenAsync()
        {
            // For integration tests, we'll use a placeholder token
            // In a real implementation, this would login with test client credentials
            return "client-jwt-token";
        }

        public async Task<string> GetExpertTokenAsync()
        {
            // For integration tests, we'll use a placeholder token
            // In a real implementation, this would login with test expert credentials
            return "expert-jwt-token";
        }
    }
}