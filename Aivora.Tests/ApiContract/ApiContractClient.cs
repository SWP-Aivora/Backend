using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;

namespace Aivora.Tests.ApiContract;

public class ApiContractClient
{
    private readonly HttpClient _httpClient;
    private string? _token;

    public ApiContractClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task LoginAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        _token = data.GetProperty("accessToken").GetString();
    }

    public async Task LoginAsClientAsync()
    {
        await LoginAsync("client@aivora.com", "password123");
    }

    public async Task LoginAsExpertAsync()
    {
        await LoginAsync("expert@aivora.com", "password123");
    }

    public async Task LoginAsAdminAsync()
    {
        await LoginAsync("admin@aivora.com", "password123");
    }

    public void Logout()
    {
        _token = null;
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
    }

    public async Task<(HttpResponseMessage Message, JsonElement? Body)> GetAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PostAsync<T>(string path, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PutAsync<T>(string path, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(payload)
        };
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PutEmptyAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path);
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PatchAsync<T>(string path, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(payload)
        };
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<(HttpResponseMessage Message, JsonElement? Body)> DeleteAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    public async Task<(HttpResponseMessage Message, JsonElement? Body)> PostMultipartAsync(string path, string fileContent, string fileName, string fieldName = "file")
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
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, fieldName, fileName);

        request.Content = content;
        var response = await _httpClient.SendAsync(request);
        var body = await TryReadBodyAsync(response);
        return (response, body);
    }

    private async Task<JsonElement?> TryReadBodyAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}
