# Environment Variables Reference

> **Source of truth:** `Aivora.api/appsettings.Development.json` + `Program.cs` required config validation.
> **Variable naming:** Use `__` as section separator (e.g., `ConnectionStrings__DefaultConnection`). Compatible with all hosting providers (Render, Railway, local Docker, etc.)

---

## Required Variables

These variables **must** be set or the application will crash at startup with `InvalidOperationException`.

| Variable | Config Key | Example | Description |
|----------|-----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | `Host=localhost;Port=5432;Database=aivora;Username=postgres;Password=secret;` | PostgreSQL connection string |
| `JwtSettings__Secret` | `JwtSettings:Secret` | `your-super-secret-key-min-32-chars!` | JWT signing secret (min 32 chars) |
| `JwtSettings__Issuer` | `JwtSettings:Issuer` | `Aivora` | JWT issuer claim |
| `JwtSettings__Audience` | `JwtSettings:Audience` | `Aivora` | JWT audience claim |
| `JwtSettings__ExpiryInMinutes` | `JwtSettings:ExpiryInMinutes` | `60` | JWT expiry in minutes (integer) |
| `CloudinaryOptions__CloudName` | `CloudinaryOptions:CloudName` | `dxxxxx` | Cloudinary cloud name |
| `CloudinaryOptions__ApiKey` | `CloudinaryOptions:ApiKey` | `123456789` | Cloudinary API key |
| `CloudinaryOptions__ApiSecret` | `CloudinaryOptions:ApiSecret` | `abc123secret` | Cloudinary API secret |

---

## Optional Variables

| Variable | Config Key | Default | Description |
|----------|-----------|---------|-------------|
| `AIProvider__Provider` | `AIProvider:Provider` | `Mock` | AI provider: `Mock` or `Gemini` |
| `AIProvider__ApiKey` | `AIProvider:ApiKey` | *(empty)* | Gemini API key. When empty, falls back to Mock provider |
| `AIProvider__BaseUrl` | `AIProvider:BaseUrl` | `https://generativelanguage.googleapis.com` | Gemini API base URL |
| `AIProvider__Model` | `AIProvider:Model` | `gemini-2.5-flash` | Gemini model name |
| `AIProvider__EnableFallback` | `AIProvider:EnableFallback` | `true` | Enable fallback to Mock when Gemini fails |
| `RateLimit__Strict__PermitLimit` | `RateLimit:Strict:PermitLimit` | `10` | Strict policy: max requests per window |
| `RateLimit__Strict__WindowInMinutes` | `RateLimit:Strict:WindowInMinutes` | `1` | Strict policy: window size in minutes |
| `RateLimit__AI__PermitLimit` | `RateLimit:AI:PermitLimit` | `20` | AI policy: max requests per window |
| `RateLimit__AI__WindowInMinutes` | `RateLimit:AI:WindowInMinutes` | `1` | AI policy: window size in minutes |
| `RateLimit__General__PermitLimit` | `RateLimit:General:PermitLimit` | `100` | General policy: max requests per window |
| `RateLimit__General__WindowInMinutes` | `RateLimit:General:WindowInMinutes` | `1` | General policy: window size in minutes |
| `SeedForceReset` | `SeedForceReset` | `false` | When `true`, deletes all data and re-seeds on startup |

---

## Placeholder Detection

The app checks for placeholder values at startup. If a config value contains any of these strings, startup will fail:

- `__SET`
- `CHANGE_ME`
- `PLACEHOLDER`

Make sure to replace all placeholder values in `appsettings.Development.json` with real values for local development.

---

## Local Development Setup

1. Copy `appsettings.Development.json` values to environment variables or user secrets:
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=aivora;Username=postgres;Password=yourpassword"
   dotnet user-secrets set "JwtSettings:Secret" "your-super-secret-key-at-least-32-characters-long!"
   dotnet user-secrets set "JwtSettings:Issuer" "Aivora"
   dotnet user-secrets set "JwtSettings:Audience" "Aivora"
   dotnet user-secrets set "JwtSettings:ExpiryInMinutes" "60"
   dotnet user-secrets set "CloudinaryOptions:CloudName" "your-cloud-name"
   dotnet user-secrets set "CloudinaryOptions:ApiKey" "your-api-key"
   dotnet user-secrets set "CloudinaryOptions:ApiSecret" "your-api-secret"
   ```

2. For Render/cloud deployment, set environment variables in the dashboard using the `__` separator format.

---

## Rate Limit Policies

| Policy | Target Endpoints | Default Limit | Window |
|--------|-----------------|---------------|--------|
| `Strict` | `AuthController` (login/register) | 10 requests | 1 minute |
| `AI` | `AIController` (Gemini calls) | 20 requests | 1 minute |
| `General` | All other controllers | 100 requests | 1 minute |

Rejection response: `429 Too Many Requests` with JSON body:
```json
{ "message": "Too many requests. Please try again after X second(s)." }
```
