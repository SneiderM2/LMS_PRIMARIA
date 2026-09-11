using System.Diagnostics;
using System.Globalization;

namespace LMS.API.Middleware;

/// <summary>
/// Middleware que registra cada petición HTTP (acceso + errores) en archivos .log locales.
/// Equivalente al logger.php con access.log para entornos .NET/Kestrel.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestTime = DateTime.Now;

        try
        {
            await _next(context);
            stopwatch.Stop();

            var logEntry = FormatAccessLog(context, requestTime, stopwatch.ElapsedMilliseconds);
            await FileLogger.WriteAsync("access", logEntry);

            // Registrar errores HTTP (4xx, 5xx)
            if (context.Response.StatusCode >= 400)
            {
                var errorEntry = FormatErrorLog(context, requestTime, stopwatch.ElapsedMilliseconds);
                await FileLogger.WriteAsync("error", errorEntry);
                _logger.LogWarning("HTTP {StatusCode} → {Method} {Path} ({Elapsed}ms)",
                    context.Response.StatusCode, context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Registrar excepción no controlada
            var errorEntry = FormatExceptionLog(context, requestTime, ex, stopwatch.ElapsedMilliseconds);
            await FileLogger.WriteAsync("error", errorEntry);

            _logger.LogError(ex, "Excepción no controlada en {Method} {Path}", context.Request.Method, context.Request.Path);

            // Re-lanzar para que el pipeline de excepciones de ASP.NET lo maneje
            throw;
        }
    }

    private static string FormatAccessLog(HttpContext ctx, DateTime time, long elapsedMs)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var method = ctx.Request.Method;
        var path = ctx.Request.Path + ctx.Request.QueryString;
        var status = ctx.Response.StatusCode;
        var userAgent = ctx.Request.Headers.UserAgent.ToString();
        var userId = ctx.User.Identity?.IsAuthenticated == true
            ? ctx.User.FindFirst("sub")?.Value ?? ctx.User.Identity.Name ?? "-"
            : "-";

        return $"[{time:yyyy-MM-dd HH:mm:ss}] {ip} | {method} {path} | {status} | {elapsedMs}ms | User:{userId} | {userAgent}";
    }

    private static string FormatErrorLog(HttpContext ctx, DateTime time, long elapsedMs)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var method = ctx.Request.Method;
        var path = ctx.Request.Path + ctx.Request.QueryString;
        var status = ctx.Response.StatusCode;

        return $"[{time:yyyy-MM-dd HH:mm:ss}] [HTTP-{status}] {ip} | {method} {path} | {elapsedMs}ms";
    }

    private static string FormatExceptionLog(HttpContext ctx, DateTime time, Exception ex, long elapsedMs)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var method = ctx.Request.Method;
        var path = ctx.Request.Path + ctx.Request.QueryString;

        return $"[{time:yyyy-MM-dd HH:mm:ss}] [EXCEPTION] {ip} | {method} {path} | {elapsedMs}ms | {ex.GetType().Name}: {ex.Message}\n    StackTrace: {ex.StackTrace}";
    }
}

/// <summary>
/// Escritor de logs a archivos locales con rotación automática por tamaño.
/// Thread-safe mediante SemaphoreSlim.
/// </summary>
public static class FileLogger
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

    /// <summary>Tamaño máximo por archivo antes de rotar (5 MB).</summary>
    private const long MaxFileSize = 5 * 1024 * 1024;

    /// <summary>Cantidad máxima de archivos rotados a conservar.</summary>
    private const int MaxRotatedFiles = 10;

    /// <summary>
    /// Cambiar el directorio de logs (llamar antes del primer uso).
    /// </summary>
    public static void SetLogDirectory(string path)
    {
        _logDirectory = path;
    }

    /// <summary>
    /// Obtiene el directorio configurado para los archivos de log.
    /// </summary>
    public static string LogDirectory => _logDirectory;

    /// <summary>
    /// Escribir una línea en el archivo de log especificado.
    /// </summary>
    /// <param name="channel">Nombre del archivo sin extensión (ej. "access", "error", "app").</param>
    /// <param name="message">Línea de texto a escribir.</param>
    public static async Task WriteAsync(string channel, string message)
    {
        await _semaphore.WaitAsync();
        try
        {
            Directory.CreateDirectory(_logDirectory);

            var filePath = Path.Combine(_logDirectory, $"{channel}.log");

            // Rotación si es necesario
            RotateIfNeeded(filePath);

            await File.AppendAllTextAsync(filePath, message + Environment.NewLine);
        }
        catch
        {
            // El logging nunca debe romper la aplicación
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Escribir un mensaje con nivel de severidad (para uso directo desde servicios).
    /// </summary>
    public static async Task LogAsync(string level, string message, object? context = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var pid = Environment.ProcessId;
        var contextStr = context != null
            ? $" | {System.Text.Json.JsonSerializer.Serialize(context)}"
            : "";

        var line = $"[{timestamp}] [{level}] [PID:{pid}] {message}{contextStr}";
        await WriteAsync("app", line);
    }

    // ── Atajos por nivel ───────────────────────────────────

    public static Task DebugAsync(string message, object? context = null) =>
        LogAsync("DEBUG", message, context);

    public static Task InfoAsync(string message, object? context = null) =>
        LogAsync("INFO", message, context);

    public static Task WarningAsync(string message, object? context = null) =>
        LogAsync("WARNING", message, context);

    public static Task ErrorAsync(string message, object? context = null) =>
        LogAsync("ERROR", message, context);

    public static Task FatalAsync(string message, object? context = null) =>
        LogAsync("FATAL", message, context);

    public static Task ExceptionAsync(Exception ex, string? extraMessage = null) =>
        LogAsync("ERROR", $"{extraMessage ?? ex.Message}", new
        {
            exception = ex.GetType().FullName,
            message = ex.Message,
            stackTrace = ex.StackTrace
        });

    // ── Rotación de archivos ───────────────────────────────

    private static void RotateIfNeeded(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < MaxFileSize)
            return;

        var dir = Path.GetDirectoryName(filePath)!;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);

        // Eliminar el archivo más antiguo
        var oldest = Path.Combine(dir, $"{name}.{MaxRotatedFiles}{ext}");
        if (File.Exists(oldest))
            File.Delete(oldest);

        // Desplazar archivos rotados (N → N+1)
        for (var i = MaxRotatedFiles - 1; i >= 1; i--)
        {
            var src = Path.Combine(dir, $"{name}.{i}{ext}");
            var dst = Path.Combine(dir, $"{name}.{i + 1}{ext}");
            if (File.Exists(src))
                File.Move(src, dst);
        }

        // Actual → .1
        File.Move(filePath, Path.Combine(dir, $"{name}.1{ext}"));
    }
}

/// <summary>
/// Extensión para registrar el middleware de logging en Program.cs.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
