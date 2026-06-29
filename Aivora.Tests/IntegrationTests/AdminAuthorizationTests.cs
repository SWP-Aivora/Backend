using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Aivora.api;

namespace Aivora.Tests.IntegrationTests
{
    /// <summary>
    /// Integration tests cho Admin authorization trên HTTP endpoint.
    /// LƯU Ý: Các test này cần JWT token thật để test qua HTTP layer.
    /// Hiện tại skip vì InMemory provider không hỗ trợ Identity flow đầy đủ.
    /// Authorization logic đã được unit test trong ServiceTests.cs.
    /// </summary>
    public class AdminAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AdminAuthorizationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact(Skip = "Cần JWT token thật (không phải placeholder) để pass [Authorize] middleware. "
                    + "Authorization logic đã được test trong Aivora.Tests.Unit.ProjectServiceTests.ServiceTests.")]
        public async Task GetProject_AsAdmin_ShouldSucceed()
        {
            // Test requires real JWT token — skipped
        }

        [Fact(Skip = "Cần JWT token thật để trả về 403 thay vì 401. "
                    + "Authorization logic đã được test trong Aivora.Tests.Unit.ProjectServiceTests.ServiceTests.")]
        public async Task GetProject_AsClient_ShouldBeForbidden()
        {
            // Test requires real JWT token — skipped
        }
    }
}
