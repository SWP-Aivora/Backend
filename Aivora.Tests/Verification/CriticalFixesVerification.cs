using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Aivora.Tests.Verification
{
    public class CriticalFixesVerification
    {
        private readonly HttpClient _client;
        private readonly string _adminToken = "admin-jwt-token";

        public CriticalFixesVerification()
        {
            // Tạo HTTP client test
            _client = new HttpClient();
            _client.BaseAddress = new Uri("https://localhost:5001");
        }

        [Fact(Skip = "Chạy khi ứng dụng đang chạy")]
        public async Task VerifyAdminAuthorizationFix()
        {
            // Test 1: Admin có thể xem chi tiết project
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects/{projectId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

            var response = await _client.SendAsync(request);

            // Verify
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(content);

            Assert.True(result.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("Project retrieved successfully", result.RootElement.GetProperty("message").GetString());
        }

        [Fact(Skip = "Chạy khi ứng dụng đang chạy")]
        public async Task VerifyDisputeResolutionFix()
        {
            // Test 2: Admin có thể giải quyết tranh chấp không bị lỗi nested transaction
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/disputes/{disputeId}/resolve");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

            var requestBody = new
            {
                resolutionType = "RELEASE_TO_EXPERT",
                resolutionNote = "Test release to expert"
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _client.SendAsync(request);

            // Verify
            Assert.True(response.IsSuccessStatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(content);

            Assert.True(result.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("RESOLVED", result.RootElement.GetProperty("data").GetProperty("status").GetString());
        }

    }
}