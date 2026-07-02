using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aivora.Services.Exceptions;
using Aivora.Services.Models;

namespace Aivora.api.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = exception switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            UnauthorizedException => HttpStatusCode.Unauthorized,
            NotFoundException => HttpStatusCode.NotFound,
            ServiceUnavailableException => HttpStatusCode.ServiceUnavailable,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = (int)statusCode;

        var errorCode = statusCode switch
        {
            HttpStatusCode.BadRequest => "validation_error",
            HttpStatusCode.Unauthorized => "unauthorized",
            HttpStatusCode.NotFound => "not_found",
            HttpStatusCode.ServiceUnavailable => "service_unavailable",
            _ => "internal_server_error"
        };

        var clientMessage = statusCode == HttpStatusCode.InternalServerError
            ? "An unexpected error occurred. Please try again later."
            : exception.Message;

        var response = ApiResponseFactory.ErrorResponse(
            clientMessage,
            new { code = errorCode },
            context.TraceIdentifier
        );

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
