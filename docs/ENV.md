# Environment Variables Reference

> **Source of truth:** `.env.example`, `Aivora.api/appsettings.Development.json`, `Program.cs` config validation.
> **Variable naming:** Use `__` as section separator (e.g., `ConnectionStrings__DefaultConnection`). Compatible with all hosting providers (Render, Railway, local Docker, etc.)

---

## Required Variables

These variables **must** be set or the application will crash at startup with `InvalidOperationException`.

| Variable | Config Key | Example | Description |
|----------|-----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | `Host=localhost;Port=5432;Database=aivora;Username=postgres;Password=your_password` | PostgreSQL connection string |
| `JwtSettings__Secret` | `JwtSettings:Secret` | `Your_Super_Secret_Key_At_Least_32_Chars_Long` | JWT signing secret (min 32 chars) |
| `JwtSettings__Issuer` | `JwtSettings:Issuer` | `AivoraApi` | JWT issuer claim |
| `JwtSettings__Audience` | `JwtSettings:Audience` | `AivoraClient` | JWT audience claim |
| `JwtSettings__ExpiryInMinutes` | `JwtSettings:ExpiryInMinutes` | `1440` | JWT expiry in minutes (integer) |
| `CloudinaryOptions__CloudName` | `CloudinaryOptions:CloudName` | `your_cloud_name` | Cloudinary cloud name |
| `CloudinaryOptions__ApiKey` | `CloudinaryOptions:ApiKey` | `your_api_key` | Cloudinary API key |
| `CloudinaryOptions__ApiSecret` | `CloudinaryOptions:ApiSecret` | `your_api_secret` | Cloudinary API secret |

---

## Optional Variables

### AI Provider

| Variable | Config Key | Default | Description |
|----------|-----------|---------|-------------|
| `AIProvider__Provider` | `AIProvider:Provider` | `Gemini` | AI provider: `Mock` or `Gemini`. Selection also requires a non-empty `ApiKey` — without one, requests fall back to Mock regardless of this setting |
| `AIProvider__ApiKey` | `AIProvider:ApiKey` | *(empty)* | Gemini API key. **Required in production/staging** — without it, all AI endpoints silently serve Mock responses. When empty, falls back to Mock provider |
| `AIProvider__BaseUrl` | `AIProvider:BaseUrl` | `https://generativelanguage.googleapis.com` | Gemini API base URL |
| `AIProvider__Model` | `AIProvider:Model` | `gemini-2.5-flash` | Gemini model name |
| `AIProvider__EnableFallback` | `AIProvider:EnableFallback` | `true` | Enable fallback to Mock when Gemini fails |

`POST /ai/service-generator` responses include a `provider` field (`"gemini"` or `"mock"`) so callers can detect when a request silently served Mock output.

### Payment (VNPay + Commission)

| Variable | Config Key | Example | Description |
|----------|-----------|---------|-------------|
| `FrontendUrl` | `FrontendUrl` | `http://localhost:5173` | Used to build the VNPay return redirect URL back to the FE |
| `VNPay__TmnCode` | `VNPay:TmnCode` | `BIVWVEYB` | VNPay merchant terminal code |
| `VNPay__HashSecret` | `VNPay:HashSecret` | `B4IEDEG7MNWFS3OGH87GEFX1KVHBII6O` | VNPay secure hash secret |
| `VNPay__BaseUrl` | `VNPay:BaseUrl` | `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` | VNPay payment gateway URL |
| `VNPay__ReturnUrl` | `VNPay:ReturnUrl` | `https://localhost:5000/api/v1/wallet/vnpay-return` | Callback URL VNPay redirects the user to after payment |
| `VNPay__IpnUrl` | `VNPay:IpnUrl` | `https://localhost:5000/api/v1/wallet/vnpay-ipn` | Server-to-server IPN callback URL |
| `Commission__Rate` | `Commission:Rate` | `0.10` | Platform commission rate (10%) taken on released payments |
| `Commission__MaxDebtLimit` | `Commission:MaxDebtLimit` | `1000` | Max negative balance allowed before blocking further spend |

> Sandbox VNPay credentials in `.env.example` are test-only keys, safe to commit for local development.

### Seeding

| Variable | Config Key | Default | Description |
|----------|-----------|---------|-------------|
| `Seed__DefaultPassword` | `Seed:DefaultPassword` | `Aivora@DevSeed2026!` (hardcoded fallback) | Password for all seeded demo accounts (admin/client/expert). Only relevant where seeding runs (never on `Production` — see [ADR 0001](adr/0001-no-manual-testing-on-shared-db.md)) |

### Rate Limiting

| Variable | Config Key | Default | Description |
|----------|-----------|---------|-------------|
| `RateLimit__Strict__PermitLimit` | `RateLimit:Strict:PermitLimit` | `10` | Strict policy: max requests per window |
| `RateLimit__Strict__WindowInMinutes` | `RateLimit:Strict:WindowInMinutes` | `1` | Strict policy: window size in minutes |
| `RateLimit__AI__PermitLimit` | `RateLimit:AI:PermitLimit` | `20` | AI policy: max requests per window |
| `RateLimit__AI__WindowInMinutes` | `RateLimit:AI:WindowInMinutes` | `1` | AI policy: window size in minutes |
| `RateLimit__General__PermitLimit` | `RateLimit:General:PermitLimit` | `100` | General policy: max requests per window |
| `RateLimit__General__WindowInMinutes` | `RateLimit:General:WindowInMinutes` | `1` | General policy: window size in minutes |

---

## Placeholder Detection

The app checks for placeholder values at startup. If a config value contains any of these strings, startup will fail:

- `__SET`
- `CHANGE_ME`
- `PLACEHOLDER`

Make sure to replace all placeholder values in `appsettings.Development.json` with real values for local development.

---

## Local Development Setup

1. Copy `.env.example` to `.env` and fill in real values, **or** use `dotnet user-secrets`:
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=aivora;Username=postgres;Password=yourpassword"
   dotnet user-secrets set "JwtSettings:Secret" "your-super-secret-key-at-least-32-characters-long!"
   dotnet user-secrets set "JwtSettings:Issuer" "AivoraApi"
   dotnet user-secrets set "JwtSettings:Audience" "AivoraClient"
   dotnet user-secrets set "JwtSettings:ExpiryInMinutes" "1440"
   dotnet user-secrets set "CloudinaryOptions:CloudName" "your-cloud-name"
   dotnet user-secrets set "CloudinaryOptions:ApiKey" "your-api-key"
   dotnet user-secrets set "CloudinaryOptions:ApiSecret" "your-api-secret"
   ```

2. For Render/cloud deployment, set environment variables in the dashboard using the `__` separator format. See `render.yaml` for the current Render service definition — note it does not yet set the VNPay/Commission vars above; add them manually on the dashboard if payment features are needed in that environment.

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
