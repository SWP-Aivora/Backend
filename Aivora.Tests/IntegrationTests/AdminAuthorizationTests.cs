using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Aivora.Tests.Helpers;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Aivora.api;
using Aivora.Repositories.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services;
using Aivora.Services.DisputeService;
using Aivora.Services.Treasury;

namespace Aivora.Tests.IntegrationTests
{
    public class AdminAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly TestAuthHelper _authHelper;

        public AdminAuthorizationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Add in-memory database
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AivoraDbContext>));

                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<AivoraDbContext>(options =>
                        options.UseInMemoryDatabase("AdminAuthTests"));
                });
            });

            _client = _factory.CreateClient();
            _authHelper = new TestAuthHelper(_client);
        }

        [Fact]
        public async Task GetProject_AsAdmin_ShouldSucceed()
        {
            // Arrange - Create test data
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();

            // Create admin user
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@test.com",
                FullName = "Admin User",
                Role = UserRole.ADMIN,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(admin);

            // Create client user
            var client = new User
            {
                Id = Guid.NewGuid(),
                Email = "client@test.com",
                FullName = "Client User",
                Role = UserRole.CLIENT,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(client);

            // Create expert user
            var expert = new User
            {
                Id = Guid.NewGuid(),
                Email = "expert@test.com",
                FullName = "Expert User",
                Role = UserRole.EXPERT,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(expert);

            // Create project
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Title = "Test Project",
                Description = "Test Description",
                ClientId = client.Id,
                ExpertId = expert.Id,
                Status = ProjectStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Projects.Add(project);

            await dbContext.SaveChangesAsync();

            // Act - Access project details with admin token
            var response = await _client.GetAsync($"/api/v1/projects/{project.Id}");

            // Assert - Should succeed with 200 OK
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(content).RootElement;

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Contains("Project retrieved successfully", result.GetProperty("message").GetString());
        }

        [Fact]
        public async Task GetProject_AsClient_ShouldBeForbidden()
        {
            // Arrange - Create test data
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();

            // Create client user
            var client = new User
            {
                Id = Guid.NewGuid(),
                Email = "client@test.com",
                FullName = "Client User",
                Role = UserRole.CLIENT,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(client);

            // Create project
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Title = "Test Project",
                Description = "Test Description",
                ClientId = client.Id,
                ExpertId = Guid.NewGuid(),
                Status = ProjectStatus.ACTIVE,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Projects.Add(project);

            await dbContext.SaveChangesAsync();

            // Act - Try to access project details with client token
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "client-token");

            var response = await _client.GetAsync($"/api/v1/projects/{project.Id}");

            // Assert - Should be forbidden
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}