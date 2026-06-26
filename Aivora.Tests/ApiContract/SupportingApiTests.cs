using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Aivora.Repositories.Enums;
using FluentAssertions;
using Xunit;

using Microsoft.Extensions.DependencyInjection;

namespace Aivora.Tests.ApiContract;

public class SupportingApiTests : IClassFixture<ApiContractTestFactory>
{
    private readonly ApiContractTestFactory _factory;

    public SupportingApiTests(ApiContractTestFactory factory)
    {
        _factory = factory;
        _factory.SeedDatabase();
    }

    [Fact]
    public async Task Run_Supporting_API_Verification_Sequence()
    {
        var client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));

        // 1. POST /api/v1/auth/register
        var registerReq = new
        {
            email = "newuser@aivora.com",
            password = "password123",
            fullName = "New User",
            role = "CLIENT"
        };
        var (regRes, regBody) = await client.PostAsync("/api/v1/auth/register", registerReq);
        regRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool regSuccess = regBody?.GetProperty("success").GetBoolean() ?? false;
        var newUserIdStr = regBody?.GetProperty("data").GetProperty("userId").GetString();
        Guid.TryParse(newUserIdStr, out var newUserId);
        var regRefreshToken = regBody?.GetProperty("data").GetProperty("refreshToken").GetString();

        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/auth/register",
            200,
            (int)regRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: regSuccess && newUserId != Guid.Empty
        );

        // 2. POST /api/v1/auth/login
        var loginReq = new
        {
            email = "client@aivora.com",
            password = "password123"
        };
        var (loginRes, loginBody) = await client.PostAsync("/api/v1/auth/login", loginReq);
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool loginSuccess = loginBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/auth/login",
            200,
            (int)loginRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: loginSuccess
        );

        // 3. POST /api/v1/auth/refresh-token
        var refreshReq = new { refreshToken = regRefreshToken };
        var (refRes, refBody) = await client.PostAsync("/api/v1/auth/refresh-token", refreshReq);
        refRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool refSuccess = refBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/auth/refresh-token",
            200,
            (int)refRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: refSuccess
        );

        // 4. GET /api/v1/auth/me
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var (meRes, meBody) = await client.GetAsync("/api/v1/auth/me");
        meRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool meSuccess = meBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/auth/me",
            200,
            (int)meRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: meSuccess
        );

        // 5. GET /api/v1/profiles/client
        var (cProfRes, cProfBody) = await client.GetAsync("/api/v1/profiles/client");
        cProfRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool cProfSuccess = cProfBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/profiles/client",
            200,
            (int)cProfRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: cProfSuccess
        );

        // 6. PUT /api/v1/profiles/client
        var updateCProfReq = new
        {
            companyName = "New Client Corp",
            industry = "Technology",
            companySize = "1-10",
            website = "https://newclient.com",
            description = "New description"
        };
        var (upCProfRes, upCProfBody) = await client.PutAsync("/api/v1/profiles/client", updateCProfReq);
        upCProfRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool upCProfSuccess = upCProfBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "PUT",
            "/api/v1/profiles/client",
            200,
            (int)upCProfRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: upCProfSuccess
        );

        // 7. GET /api/v1/profiles/expert
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.EXPERT));
        var (eProfRes, eProfBody) = await client.GetAsync("/api/v1/profiles/expert");
        eProfRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool eProfSuccess = eProfBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/profiles/expert",
            200,
            (int)eProfRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: eProfSuccess
        );

        // 8. PUT /api/v1/profiles/expert
        var updateEProfReq = new
        {
            title = "Lead Architect",
            bio = "Experienced developer",
            hourlyRate = 50.00,
            experienceYears = 8,
            availabilityStatus = "AVAILABLE"
        };
        var (upEProfRes, upEProfBody) = await client.PutAsync("/api/v1/profiles/expert", updateEProfReq);
        upEProfRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool upEProfSuccess = upEProfBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "PUT",
            "/api/v1/profiles/expert",
            200,
            (int)upEProfRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: upEProfSuccess
        );

        // 9. GET /api/v1/profiles/expert/{expertId}
        client.Logout();
        var (pubEProfRes, pubEProfBody) = await client.GetAsync($"/api/v1/profiles/expert/{ApiContractTestData.ExpertUserId}");
        pubEProfRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool pubEProfSuccess = pubEProfBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/profiles/expert/{expertId}",
            200,
            (int)pubEProfRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: pubEProfSuccess
        );

        // 10. GET /api/v1/profiles/experts/featured
        var (featRes, featBody) = await client.GetAsync("/api/v1/profiles/experts/featured?count=3");
        featRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool featSuccess = featBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/profiles/experts/featured",
            200,
            (int)featRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: featSuccess
        );

        // 11. PUT /api/v1/users/me
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var upUserReq = new
        {
            fullName = "Aivora Client Updated",
            phone = "123456789"
        };
        var (upUserRes, upUserBody) = await client.PutAsync("/api/v1/users/me", upUserReq);
        upUserRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool upUserSuccess = upUserBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "PUT",
            "/api/v1/users/me",
            200,
            (int)upUserRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: upUserSuccess
        );

        // 12. GET /api/v1/categories
        client.Logout();
        var (catRes, catBody) = await client.GetAsync("/api/v1/categories");
        catRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool catSuccess = catBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/categories",
            200,
            (int)catRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: catSuccess
        );

        // 13. GET /api/v1/categories/{id}
        var (catIdRes, catIdBody) = await client.GetAsync($"/api/v1/categories/{ApiContractTestData.CategoryId}");
        catIdRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool catIdSuccess = catIdBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/categories/{id}",
            200,
            (int)catIdRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: catIdSuccess
        );

        // 14. POST /api/v1/categories (Admin only)
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.ADMIN));
        var createCatReq = new
        {
            name = "Cloud Services",
            description = "AWS and Azure skills"
        };
        var (cCatRes, cCatBody) = await client.PostAsync("/api/v1/categories", createCatReq);
        cCatRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool cCatSuccess = cCatBody?.GetProperty("success").GetBoolean() ?? false;
        var newCatIdStr = cCatBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(newCatIdStr, out var newCatId);

        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/categories",
            200,
            (int)cCatRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: cCatSuccess && newCatId != Guid.Empty
        );

        // 15. GET /api/v1/skills
        client.Logout();
        var (skillsRes, skillsBody) = await client.GetAsync("/api/v1/skills");
        skillsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool skillsSuccess = skillsBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/skills",
            200,
            (int)skillsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: skillsSuccess
        );

        // 16. GET /api/v1/skills/{id}
        var (skillIdRes, skillIdBody) = await client.GetAsync($"/api/v1/skills/{ApiContractTestData.SkillId}");
        skillIdRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool skillIdSuccess = skillIdBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/skills/{id}",
            200,
            (int)skillIdRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: skillIdSuccess
        );

        // 17. POST /api/v1/skills (Admin only)
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.ADMIN));
        var createSkillReq = new
        {
            name = "Kubernetes",
            categoryId = newCatId
        };
        var (cSkillRes, cSkillBody) = await client.PostAsync("/api/v1/skills", createSkillReq);
        cSkillRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool cSkillSuccess = cSkillBody?.GetProperty("success").GetBoolean() ?? false;
        var newSkillIdStr = cSkillBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(newSkillIdStr, out var newSkillId);

        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/skills",
            200,
            (int)cSkillRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: cSkillSuccess && newSkillId != Guid.Empty
        );

        // 18. POST /api/v1/skills/expert/me (Expert adds skill)
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.EXPERT));
        var addExpSkillReq = new
        {
            skillId = ApiContractTestData.SkillId,
            level = "ADVANCED",
            yearsExperience = 3
        };
        var (addESRes, addESBody) = await client.PostAsync("/api/v1/skills/expert/me", addExpSkillReq);
        addESRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool addESSuccess = addESBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/skills/expert/me",
            200,
            (int)addESRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: addESSuccess
        );

        // 19. DELETE /api/v1/skills/expert/me/{skillId}
        var (delESRes, delESBody) = await client.DeleteAsync($"/api/v1/skills/expert/me/{ApiContractTestData.SkillId}");
        delESRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool delESSuccess = delESBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "DELETE",
            "/api/v1/skills/expert/me/{skillId}",
            200,
            (int)delESRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: delESSuccess
        );

        // 20. POST /api/v1/conversations/init
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var (chatInitRes, chatInitBody) = await client.PostAsync($"/api/v1/conversations/init?expertId={ApiContractTestData.ExpertUserId}", new { });
        chatInitRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool chatInitSuccess = chatInitBody?.GetProperty("success").GetBoolean() ?? false;
        var conversationIdStr = chatInitBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(conversationIdStr, out var conversationId);

        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/conversations/init",
            200,
            (int)chatInitRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: chatInitSuccess && conversationId != Guid.Empty
        );

        // 21. GET /api/v1/conversations
        var (listChatsRes, listChatsBody) = await client.GetAsync("/api/v1/conversations?page=1&pageSize=10");
        listChatsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool listChatsSuccess = listChatsBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/conversations",
            200,
            (int)listChatsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: listChatsSuccess
        );

        // 22. GET /api/v1/conversations/{id}/messages
        var (listMsgsRes, listMsgsBody) = await client.GetAsync($"/api/v1/conversations/{conversationId}/messages?page=1&pageSize=10");
        listMsgsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool listMsgsSuccess = listMsgsBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/conversations/{id}/messages",
            200,
            (int)listMsgsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: listMsgsSuccess
        );

        // 23. POST /api/v1/conversations/{id}/read
        var (readChatRes, readChatBody) = await client.PostAsync($"/api/v1/conversations/{conversationId}/read", new { });
        readChatRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool readChatSuccess = readChatBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/conversations/{id}/read",
            200,
            (int)readChatRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: readChatSuccess
        );

        // 24. POST /api/v1/media/upload-image
        var (upImgRes, upImgBody) = await client.PostMultipartAsync("/api/v1/media/upload-image?folder=test", "fake image contents", "avatar.png");
        upImgRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool upImgSuccess = upImgBody?.GetProperty("success").GetBoolean() ?? false;
        var imgPublicId = upImgBody?.GetProperty("data").GetProperty("publicId").GetString();

        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/media/upload-image",
            200,
            (int)upImgRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: upImgSuccess
        );

        // 25. POST /api/v1/media/upload-file
        var (upFileRes, upFileBody) = await client.PostMultipartAsync("/api/v1/media/upload-file?folder=test", "fake file contents", "resume.pdf");
        upFileRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool upFileSuccess = upFileBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "POST",
            "/api/v1/media/upload-file",
            200,
            (int)upFileRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: upFileSuccess
        );

        // 26. DELETE /api/v1/media/{publicId}
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.ADMIN));
        var (delMediaRes, delMediaBody) = await client.DeleteAsync($"/api/v1/media/{imgPublicId}");
        delMediaRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool delMediaSuccess = delMediaBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "DELETE",
            "/api/v1/media/{publicId}",
            200,
            (int)delMediaRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: delMediaSuccess
        );

        // 27. GET /api/v1/notifications
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var (listNotRes, listNotBody) = await client.GetAsync("/api/v1/notifications?page=1&pageSize=10");
        listNotRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool listNotSuccess = listNotBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/notifications",
            200,
            (int)listNotRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: listNotSuccess
        );

        // 28. GET /api/v1/notifications/unread-count
        var (unNotRes, unNotBody) = await client.GetAsync("/api/v1/notifications/unread-count");
        unNotRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool unNotSuccess = unNotBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/notifications/unread-count",
            200,
            (int)unNotRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: unNotSuccess
        );

        // 29. PUT /api/v1/notifications/read-all
        var (readAllNotRes, readAllNotBody) = await client.PutEmptyAsync("/api/v1/notifications/read-all");
        readAllNotRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool readAllNotSuccess = readAllNotBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "PUT",
            "/api/v1/notifications/read-all",
            200,
            (int)readAllNotRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: readAllNotSuccess
        );

        // 30. PUT /api/v1/notifications/{id}/read
        // Since we don't have a specific notification ID, we can record it as TRUE since read-all works and code matches,
        // or we can seed a notification. Let's retrieve notifications first to see if there is any to mark as read.
        var firstNotId = Guid.Empty;
        var nots = listNotBody?.GetProperty("data").GetProperty("items");
        if (nots != null && nots.Value.GetArrayLength() > 0)
        {
            var idStr = nots.Value[0].GetProperty("id").GetString();
            Guid.TryParse(idStr, out firstNotId);
        }
        if (firstNotId != Guid.Empty)
        {
            var (readNotRes, readNotBody) = await client.PutEmptyAsync($"/api/v1/notifications/{firstNotId}/read");
            _factory.Tracker.Record(
                "Supporting",
                "PUT",
                "/api/v1/notifications/{id}/read",
                200,
                (int)readNotRes.StatusCode,
                requestMatchesDoc: true,
                responseMatchesDoc: readNotBody?.GetProperty("success").GetBoolean() ?? false
            );
        }
        else
        {
            // Seed a notification in DB and test it
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Aivora.Repositories.Data.AivoraDbContext>();
                var notObj = new Aivora.Repositories.Entities.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = ApiContractTestData.ClientUserId,
                    Title = "Test Not",
                    Message = "Test message",
                    IsRead = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.Notifications.Add(notObj);
                db.SaveChanges();
                firstNotId = notObj.Id;
            }
            var (readNotRes, readNotBody) = await client.PutEmptyAsync($"/api/v1/notifications/{firstNotId}/read");
            _factory.Tracker.Record(
                "Supporting",
                "PUT",
                "/api/v1/notifications/{id}/read",
                200,
                (int)readNotRes.StatusCode,
                requestMatchesDoc: true,
                responseMatchesDoc: readNotBody?.GetProperty("success").GetBoolean() ?? false
            );
        }

        // 31. GET /api/v1/admin/stats
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.ADMIN));
        var (statsRes, statsBody) = await client.GetAsync("/api/v1/admin/stats");
        statsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool statsSuccess = statsBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/admin/stats",
            200,
            (int)statsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: statsSuccess
        );

        // 32. GET /api/v1/admin/users
        var (admUsersRes, admUsersBody) = await client.GetAsync("/api/v1/admin/users?page=1&pageSize=10");
        admUsersRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool admUsersSuccess = admUsersBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "GET",
            "/api/v1/admin/users",
            200,
            (int)admUsersRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: admUsersSuccess
        );

        // 33. PUT /api/v1/admin/users/{id}/suspend
        var suspReq = new { reason = "Violating terms" };
        var (suspUserRes, suspUserBody) = await client.PutAsync($"/api/v1/admin/users/{ApiContractTestData.ExpertUserId}/suspend", suspReq);
        suspUserRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool suspUserSuccess = suspUserBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "PUT",
            "/api/v1/admin/users/{id}/suspend",
            200,
            (int)suspUserRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: suspUserSuccess
        );

        // 34. PUT /api/v1/admin/users/{id}/unsuspend
        var (unsuspUserRes, unsuspUserBody) = await client.PutEmptyAsync($"/api/v1/admin/users/{ApiContractTestData.ExpertUserId}/unsuspend");
        unsuspUserRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool unsuspUserSuccess = unsuspUserBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Supporting",
            "PUT",
            "/api/v1/admin/users/{id}/unsuspend",
            200,
            (int)unsuspUserRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: unsuspUserSuccess
        );

        // Export all results at the end of the test run
        _factory.Tracker.ExportResults();
    }
}
