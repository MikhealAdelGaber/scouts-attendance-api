using System.Net;
using System.Text.Json;
using ScoutsAttendance.Application.Common;

namespace ScoutsAttendance.API.Middleware;

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
            _logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        (int statusCode, string message) = ex switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, ex.Message),
            InvalidOperationException => (StatusCodes.Status400BadRequest, ex.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, $"{ex.GetType().Name}: {ex.Message}")
        };

        context.Response.StatusCode = statusCode;
        var response = ApiResponse.Fail(message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
