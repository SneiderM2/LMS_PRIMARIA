using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LMS.API.Middleware;

/// <summary>
/// Modelo estructurado para registros de auditoría global (.NET 10 / Kestrel).
/// </summary>
public class AuditLogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

    [JsonPropertyName("level")]
    public string Level { get; set; } = "INFO"; // INFO, AUDIT, WARNING, ERROR

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; set; }

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "0.0.0.0";

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("userEmail")]
    public string? UserEmail { get; set; }

    [JsonPropertyName("userRole")]
    public string? UserRole { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}

public class AuditLogResult
{
    [JsonPropertyName("items")]
    public List<AuditLogEntry> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}

/// <summary>
/// Middleware que registra cada petición HTTP, eventos de autenticación, errores
/// y operaciones CRUD en logs tradicionales y en /logs/app-audit.log estructurado en JSON.
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
        // Ignorar endpoints de Swagger o sondeos técnicos repetitivos
        var pathValue = context.Request.Path.Value ?? string.Empty;
        if (pathValue.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            pathValue.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestTime = DateTime.Now;
        Exception? caughtException = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            caughtException = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var statusCode = caughtException != null ? 500 : context.Response.StatusCode;

            // 1. Logs de texto convencionales (access.log y error.log)
            try
            {
                var logEntry = FormatAccessLog(context, requestTime, elapsedMs);
                await FileLogger.WriteAsync("access", logEntry);

                if (statusCode >= 400 || caughtException != null)
                {
                    var errorEntry = caughtException != null
                        ? FormatExceptionLog(context, requestTime, caughtException, elapsedMs)
                        : FormatErrorLog(context, requestTime, elapsedMs);

                    await FileLogger.WriteAsync("error", errorEntry);

                    if (caughtException != null)
                    {
                        _logger.LogError(caughtException, "Excepción no controlada en {Method} {Path}", context.Request.Method, context.Request.Path);
                    }
                    else
                    {
                        _logger.LogWarning("HTTP {StatusCode} → {Method} {Path} ({Elapsed}ms)",
                            statusCode, context.Request.Method, context.Request.Path, elapsedMs);
                    }
                }
            }
            catch
            {
                // El logging no debe interrumpir el flujo
            }

            // 2. Registro estructurado en app-audit.log
            try
            {
                var auditEntry = BuildAuditEntry(context, requestTime, elapsedMs, statusCode, caughtException);
                await FileLogger.WriteAuditAsync(auditEntry);
            }
            catch
            {
                // Respaldo silencioso
            }
        }
    }

    private static AuditLogEntry BuildAuditEntry(
        HttpContext ctx, 
        DateTime time, 
        long elapsedMs, 
        int statusCode, 
        Exception? ex)
    {
        // Extracción de IP (considerando proxies X-Forwarded-For de Render / Nginx)
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrWhiteSpace(forwarded))
        {
            ip = forwarded.ToString().Split(',')[0].Trim();
        }

        var method = ctx.Request.Method;
        var path = ctx.Request.Path.Value ?? string.Empty;

        // Claims del usuario autenticado
        var user = ctx.User;
        string? userId = null;
        string? userEmail = null;
        string? userRole = null;

        if (user.Identity?.IsAuthenticated == true)
        {
            userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? user.FindFirst("sub")?.Value 
                     ?? user.Identity.Name;

            userEmail = user.FindFirst(ClaimTypes.Email)?.Value 
                        ?? (user.Identity.Name?.Contains('@') == true ? user.Identity.Name : null);

            userRole = user.FindFirst(ClaimTypes.Role)?.Value 
                       ?? user.FindFirst("role")?.Value;
        }

        // Determinación de Nivel (Level)
        string level;
        if (statusCode >= 500 || ex != null)
        {
            level = "ERROR";
        }
        else if (statusCode >= 400)
        {
            level = "WARNING";
        }
        else if (method != "GET" || path.Contains("/auth/") || path.Contains("/admin/"))
        {
            level = "AUDIT";
        }
        else
        {
            level = "INFO";
        }

        // Resolución de Acción descriptiva en lenguaje humano
        var action = ResolveActionName(method, path, statusCode);

        var details = ex != null
            ? $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
            : (statusCode >= 400 ? $"HTTP {statusCode} - Respuesta con error en {method} {path}" : null);

        return new AuditLogEntry
        {
            Timestamp = time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Level = level,
            Method = method,
            Path = path + ctx.Request.QueryString,
            StatusCode = statusCode,
            ElapsedMs = elapsedMs,
            Ip = ip,
            UserId = userId,
            UserEmail = userEmail,
            UserRole = userRole,
            Action = action,
            Details = details
        };
    }

    private static string ResolveActionName(string method, string path, int status)
    {
        var p = path.ToLowerInvariant();

        if (p.EndsWith("/auth/login"))
        {
            if (status == 200) return "Auth: Inicio de Sesión Exitoso";
            if (status == 409) return "Auth: Alerta - Doble Sesión Activa Detectada";
            if (status == 423) return "Auth: Cuenta Bloqueada por Intentos Fallidos";
            return "Auth: Intento de Inicio de Sesión Fallido";
        }
        if (p.EndsWith("/auth/google-login"))
        {
            return status == 200 ? "Auth: Login Exitoso con Google OAuth" : "Auth: Fallo en Login con Google";
        }
        if (p.EndsWith("/auth/force-login"))
        {
            return "Auth: Cierre de Sesión Remota y Forzado de Acceso";
        }
        if (p.EndsWith("/auth/register"))
        {
            return status == 201 ? "Auth: Registro de Nueva Cuenta Escolar" : "Auth: Intento de Registro";
        }
        if (p.EndsWith("/auth/logout"))
        {
            return "Auth: Cierre de Sesión Voluntario";
        }

        // Operaciones Administrativas
        if (p.Contains("/admin/backup-sql")) return "Admin: Descarga de Respaldo SQL de Base de Datos";
        if (p.Contains("/admin/restore-backup")) return "Admin: Restauración de Base de Datos desde Respaldo";
        if (p.Contains("/admin/audit-logs")) return "Admin: Consulta de Logs de Auditoría Global";
        if (p.Contains("/systemfiles/logs")) return "Admin: Inspección de Archivos de Logs";
        if (p.Contains("/systemfiles/htaccess")) return "Admin: Inspección de Configuración del Servidor";

        // Operaciones CRUD
        if (p.Contains("/admin/users") || p.Contains("/usuarios"))
        {
            if (method == "POST") return "Admin: Creación de Nuevo Usuario";
            if (method == "PUT") return "Admin: Actualización de Datos de Usuario";
            if (method == "DELETE") return "Admin: Eliminación / Baja de Usuario";
            return "Admin: Consulta de Directorio de Usuarios";
        }

        if (p.Contains("/cursos") || p.Contains("/courses"))
        {
            if (method == "POST") return "Gestión Académica: Creación de Materia/Curso";
            if (method == "PUT") return "Gestión Académica: Edición de Materia/Curso";
            if (method == "DELETE") return "Gestión Académica: Eliminación de Materia/Curso";
            return "Académico: Consulta de Cursos";
        }

        if (p.Contains("/actividades") || p.Contains("/tasks"))
        {
            if (method == "POST") return "Docente: Creación de Nueva Actividad/Tarea";
            if (method == "PUT") return "Docente: Edición de Actividad/Tarea";
            return "Académico: Consulta de Actividades";
        }

        if (p.Contains("/entregas") || p.Contains("/submissions"))
        {
            if (method == "POST") return "Estudiante: Envío de Tarea / Entrega Académica";
            return "Académico: Revisión de Entregas";
        }

        return $"{method} {path}";
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
/// Escritor y lector de logs estructurados a archivos locales con rotación automática por tamaño.
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

    public static void SetLogDirectory(string path)
    {
        _logDirectory = path;
    }

    public static string LogDirectory => _logDirectory;

    public static string GetAuditLogPath() => Path.Combine(_logDirectory, "app-audit.log");

    /// <summary>
    /// Escribir una línea en el archivo de log especificado.
    /// </summary>
    public static async Task WriteAsync(string channel, string message)
    {
        await _semaphore.WaitAsync();
        try
        {
            Directory.CreateDirectory(_logDirectory);
            var filePath = Path.Combine(_logDirectory, $"{channel}.log");
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
    /// Escribe una entrada estructurada en /logs/app-audit.log (JSON por línea).
    /// </summary>
    public static async Task WriteAuditAsync(AuditLogEntry entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry);
            await WriteAsync("app-audit", json);
        }
        catch
        {
            // Fallo silencioso de auditoría
        }
    }

    /// <summary>
    /// Lee y pagina los logs estructurados de auditoría con soporte para filtros.
    /// </summary>
    public static async Task<AuditLogResult> ReadAuditLogsAsync(
        int page = 1,
        int pageSize = 25,
        string? level = null,
        string? keyword = null,
        string? role = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var filePath = GetAuditLogPath();
        if (!File.Exists(filePath))
        {
            return new AuditLogResult { Page = page, PageSize = pageSize, TotalCount = 0 };
        }

        var entries = new List<AuditLogEntry>();
        await _semaphore.WaitAsync();
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var item = JsonSerializer.Deserialize<AuditLogEntry>(line);
                    if (item == null) continue;

                    // Filtro de Nivel
                    if (!string.IsNullOrWhiteSpace(level) &&
                        !string.Equals(level, "ALL", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(item.Level, level, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Filtro de Rol
                    if (!string.IsNullOrWhiteSpace(role) &&
                        !string.Equals(role, "ALL", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(item.UserRole, role, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Filtro de Fecha
                    if (fromDate.HasValue || toDate.HasValue)
                    {
                        if (DateTime.TryParse(item.Timestamp, out var itemDate))
                        {
                            if (fromDate.HasValue && itemDate < fromDate.Value) continue;
                            if (toDate.HasValue && itemDate > toDate.Value) continue;
                        }
                    }

                    // Filtro por Palabra Clave
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        var kw = keyword.Trim().ToLowerInvariant();
                        var match = (item.Action?.ToLowerInvariant().Contains(kw) ?? false) ||
                                    (item.Path?.ToLowerInvariant().Contains(kw) ?? false) ||
                                    (item.UserEmail?.ToLowerInvariant().Contains(kw) ?? false) ||
                                    (item.UserId?.ToLowerInvariant().Contains(kw) ?? false) ||
                                    (item.Ip?.Contains(kw) ?? false) ||
                                    (item.Details?.ToLowerInvariant().Contains(kw) ?? false);

                        if (!match) continue;
                    }

                    entries.Add(item);
                }
                catch
                {
                    // Ignora líneas no serializadas o corruptas
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }

        // Ordenar de más reciente a más antiguo
        entries.Reverse();

        var totalCount = entries.Count;
        var pagedItems = entries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new AuditLogResult
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Escribir un mensaje con nivel de severidad en app.log
    /// </summary>
    public static async Task LogAsync(string level, string message, object? context = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var pid = Environment.ProcessId;
        var contextStr = context != null
            ? $" | {JsonSerializer.Serialize(context)}"
            : "";

        var line = $"[{timestamp}] [{level}] [PID:{pid}] {message}{contextStr}";
        await WriteAsync("app", line);
    }

    public static Task DebugAsync(string message, object? context = null) => LogAsync("DEBUG", message, context);
    public static Task InfoAsync(string message, object? context = null) => LogAsync("INFO", message, context);
    public static Task WarningAsync(string message, object? context = null) => LogAsync("WARNING", message, context);
    public static Task ErrorAsync(string message, object? context = null) => LogAsync("ERROR", message, context);
    public static Task FatalAsync(string message, object? context = null) => LogAsync("FATAL", message, context);

    public static Task ExceptionAsync(Exception ex, string? extraMessage = null) =>
        LogAsync("ERROR", $"{extraMessage ?? ex.Message}", new
        {
            exception = ex.GetType().FullName,
            message = ex.Message,
            stackTrace = ex.StackTrace
        });

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

        var oldest = Path.Combine(dir, $"{name}.{MaxRotatedFiles}{ext}");
        if (File.Exists(oldest))
            File.Delete(oldest);

        for (var i = MaxRotatedFiles - 1; i >= 1; i--)
        {
            var src = Path.Combine(dir, $"{name}.{i}{ext}");
            var dst = Path.Combine(dir, $"{name}.{i + 1}{ext}");
            if (File.Exists(src))
                File.Move(src, dst);
        }

        File.Move(filePath, Path.Combine(dir, $"{name}.1{ext}"));
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
