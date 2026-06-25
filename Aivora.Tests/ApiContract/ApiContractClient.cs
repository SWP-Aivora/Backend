using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Aivora.Tests.ApiContract;

/// <summary>
///   HTTP client wrapper cho API contract tests.
///   Tự động gắn Bearer token và cung cấp helper cho GET/POST/PUT/PATCH/DELETE.
/// </summary>
public class ApiContractClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiContractClient(HttpClient http, string? token = null)
    {
        _http = http;
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
    }

    public void SetToken(string token) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    public void ClearToken() =>
        _http.DefaultRequestHeaders.Authorization = null;

    // ── GET ───────────────────────────────────────────────────────
    public async Task<HttpResponseMessage> GetAsync(string path) =>
        await _http.GetAsync(path);

    public async Task<T?> GetAsync<T>(string path)
    {
        var response = await _http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    // ── POST ──────────────────────────────────────────────────────
    public async Task<HttpResponseMessage> PostAsync(string path, object? body = null)
    {
        if (body is null)
            return await _http.PostAsync(path, null);

        var json = JsonSerializer.Serialize(body, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _http.PostAsync(path, content);
    }

    public async Task<T?> PostAsync<T>(string path, object? body = null)
    {
        var response = await PostAsync(path, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    // ── PUT ───────────────────────────────────────────────────────
    public async Task<HttpResponseMessage> PutAsync(string path, object? body = null)
    {
        var json = JsonSerializer.Serialize(body ?? new { }, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _http.PutAsync(path, content);
    }

    public async Task<T?> PutAsync<T>(string path, object? body = null)
    {
        var response = await PutAsync(path, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    // ── PATCH ─────────────────────────────────────────────────────
    public async Task<HttpResponseMessage> PatchAsync(string path, object? body = null)
    {
        var json = JsonSerializer.Serialize(body ?? new { }, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _http.PatchAsync(path, content);
    }

    public async Task<T?> PatchAsync<T>(string path, object? body = null)
    {
        var response = await PatchAsync(path, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    // ── DELETE ────────────────────────────────────────────────────
    public async Task<HttpResponseMessage> DeleteAsync(string path) =>
        await _http.DeleteAsync(path);

    public async Task<T?> DeleteAsync<T>(string path)
    {
        var response = await _http.DeleteAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }
}
