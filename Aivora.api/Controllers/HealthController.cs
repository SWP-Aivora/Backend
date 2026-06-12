using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)] // Hide from OpenAPI/Swagger
public class HealthController : ControllerBase
{
    /// <summary>
    /// Simple health check for container orchestration (Docker, Render, k8s).
    /// Returns 200 OK if the application is running.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow,
            version = GetType().Assembly.GetName().Version?.ToString() ?? "unknown"
        });
    }
}
