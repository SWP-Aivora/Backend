---
name: api-doc
description: Đồng bộ hóa OpenAPI documentation từ Controllers
---

# API Documentation Skill

Tự động đồng bộ hóa OpenAPI documentation từ Controllers trong Aivora.api dựa trên cấu hình Scalar.

## Usage

```bash
/api-doc [action]
```

Actions:
- `sync` - Đồng bộ hóa từ Controllers
- `validate` - Validate OpenAPI spec
- `update` - Update UI documentation

## Process

1. Scan all Controllers in Aivora.api/Controllers
2. Extract endpoints, parameters, responses
3. Update OpenAPI spec via Scalar
4. Generate documentation preview

## Template for Controllers

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class ExampleController : ControllerBase
{
    /// <summary>
    /// API description here
    /// </summary>
    /// <param name="id">Parameter description</param>
    /// <returns>Response description</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ResponseType), 200)]
    public async Task<IActionResult> Get(int id)
    {
        // Implementation
    }
}
```

## Configuration

Scalar configuration in Aivora.api/Extensions/OpenApiExtensions.cs:

```csharp
services.AddOpenApiServices(options =>
{
    options.WithServers(new OpenApiServer
    {
        Url = "https://localhost:5001/api/v1",
        Description = "Development Server"
    });
});
```