using System.Net;
using System.Text.Json;

namespace LMS.API.Middleware;

/// <summary>
/// Middleware que captura excepciones no controladas, las registra en el sistema
/// de logs y devuelve una respuesta JSON estandarizada.
/// Equivalente a las directivas ErrorDocument del .htaccess para Apache.
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

        var (statusCode, title) = ex switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado"),
            KeyNotFoundException        => (HttpStatusCode.NotFound, "Recurso no encontrado"),
            ArgumentException           => (HttpStatusCode.BadRequest, "Solicitud inválida"),
            InvalidOperationException   => (HttpStatusCode.Conflict, "Operación no válida"),
            _                           => (HttpStatusCode.InternalServerError, "Error interno del servidor")
        };

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponse
        {
            Status = (int)statusCode,
            Title = title,
            Message = _env.IsDevelopment() ? ex.Message : "Ocurrió un error inesperado. Intente de nuevo más tarde.",
            Timestamp = DateTime.UtcNow,
            Path = $"{context.Request.Method} {context.Request.Path}",
            TraceId = context.TraceIdentifier
        };

        // En desarrollo incluir stack trace
        if (_env.IsDevelopment())
        {
            response.Detail = ex.StackTrace;
        }

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Modelo de respuesta estandarizada para errores HTTP.
/// </summary>
public class ErrorResponse
{
    public int Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Path { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string? Detail { get; set; }
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
