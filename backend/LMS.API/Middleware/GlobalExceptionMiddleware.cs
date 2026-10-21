using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LMS.API.Middleware;

/// <summary>
/// Middleware global de manejo de excepciones bajo el estándar RFC 7807 (Problem Details for HTTP APIs).
/// Captura excepciones no controladas, las registra en el sistema de auditoría/logs y devuelve
/// una respuesta estructurada "application/problem+json" sin filtrar trazas sensibles en producción.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        _logger.LogError(ex, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);

        // Registrar en archivo de log
        await FileLogger.ExceptionAsync(ex, $"{context.Request.Method} {context.Request.Path}");

        var (statusCode, problemType, title) = ex switch
        {
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                "No autorizado"),
            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                "Recurso no encontrado"),
            ArgumentException => (
                HttpStatusCode.BadRequest,
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                "Solicitud inválida"),
            InvalidOperationException => (
                HttpStatusCode.Conflict,
                "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                "Operación no válida"),
            _ => (
                HttpStatusCode.InternalServerError,
                "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                "Error interno del servidor")
        };

        var userMessage = _env.IsDevelopment()
            ? ex.Message
            : "Ocurrió un error inesperado al procesar la solicitud. Intente de nuevo más tarde.";

        context.Response.ContentType = "application/problem+json; charset=utf-8";
        context.Response.StatusCode = (int)statusCode;
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;

        var response = new ErrorResponse
        {
            Type = problemType,
            Title = title,
            Status = (int)statusCode,
            Detail = _env.IsDevelopment() ? $"{ex.Message} | {ex.StackTrace}" : userMessage,
            Instance = context.Request.Path.Value ?? "/",
            Message = userMessage,
            Timestamp = DateTime.UtcNow,
            Path = $"{context.Request.Method} {context.Request.Path}",
            TraceId = context.TraceIdentifier
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Modelo de respuesta estandarizada bajo RFC 7807 (Problem Details for HTTP APIs),
/// manteniendo propiedades de extensión compatibles con los clientes Angular existentes.
/// </summary>
public class ErrorResponse
{
    public string Type { get; set; } = "about:blank";
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string Instance { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Path { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
}

/// <summary>
/// Extensión para registrar el middleware de excepciones en Program.cs.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
