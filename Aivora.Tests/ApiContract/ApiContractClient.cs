using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Aivora.Tests.ApiContract;

/// <summary>
///   HTTP client wrapper cho API contract tests.
///   Hỗ trợ cả tuple pattern (Message, Body) và typed response.
/// </summary>
public class ApiContractClient
{
    private readonly HttpClient _http;
    private string? _token;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiContractClient(HttpClient http)
    {
        _http = http;
    }

    // ── Token management ──────────────────────────────────────────
    public void SetToken(string token)
    {
        _token = token;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearToken()
    {
        _token = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_token))
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);
    }

    // ── GET ───────────────────────────────────────────────────────
    public async Task<(HttpResponseMessage Message, JsonElement? Body)> GetAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuth(request);
        var response = await _http.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<T?> GetAsync<T>(string path)
    {
        var (response, _) = await GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    // ── POST ──────────────────────────────────────────────────────
    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PostAsync<T>(string path, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyAuth(request);
        var response = await _http.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<TResp?> PostAsync<TReq, TResp>(string path, TReq payload)
    {
        var (response, _) = await PostAsync(path, payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResp>(JsonOptions);
    }

    // ── PUT ───────────────────────────────────────────────────────
    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PutAsync<T>(string path, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyAuth(request);
        var response = await _http.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<TResp?> PutAsync<TReq, TResp>(string path, TReq payload)
    {
        var (response, _) = await PutAsync(path, payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResp>(JsonOptions);
    }

    // ── PATCH ─────────────────────────────────────────────────────
    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PatchAsync<T>(string path, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyAuth(request);
        var response = await _http.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<TResp?> PatchAsync<TReq, TResp>(string path, TReq payload)
    {
        var (response, _) = await PatchAsync(path, payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResp>(JsonOptions);
    }

    // ── PUT (no body) ────────────────────────────────────────────
    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PutEmptyAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path);
        ApplyAuth(request);
        var response = await _http.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    // ── DELETE ────────────────────────────────────────────────────
    public async Task<(HttpResponseMessage Message, JsonElement? Body)> DeleteAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
        ApplyAuth(request);
        var response = await _http.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<T?> DeleteAsync<T>(string path)
    {
        var (response, _) = await DeleteAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    // ── Multipart POST (file upload) ─────────────────────────────
    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PostMultipartAsync(
        string path, string fileContent, string fileName, string fieldName = "file")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        ApplyAuth(request);

        var content = new MultipartFormDataContent();
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(fileContent);
        writer.Flush();
        stream.Position = 0;

        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, fieldName, fileName);

        request.Content = content;
        var response = await _http.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private static async Task<JsonElement?> TryReadBodyAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
                return null;

            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Response body is not valid JSON (e.g., plain text, HTML error page, or empty).
            // This is expected for some endpoints — callers check HTTP status codes separately.
            return null;
        }
        catch (HttpRequestException)
        {
            // Network or HTTP-level error during body read — best-effort helper, not fatal.
            return null;
        }
    }
}
