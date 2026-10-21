using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
        // Ignorar endpoints de Swagger, health-checks y sondeos técnicos repetitivos
        var pathValue = context.Request.Path.Value ?? string.Empty;
        if (pathValue.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            pathValue.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
            pathValue.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            pathValue.Equals("/ping", StringComparison.OrdinalIgnoreCase))
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

            // 1. Logs de texto estructurados obligatorios (access.log, audit.log y error.log)
            try
            {
                var structuredLine = FormatStructuredLog(context, requestTime, statusCode, caughtException);
                await FileLogger.WriteAsync("access", structuredLine);

                // Si es un evento de modificación (POST, PUT, DELETE, PATCH) o endpoint administrativo/docente, registrar en audit.log
                if (structuredLine.Contains(" | [AUDIT] | ") || context.Request.Method is "POST" or "PUT" or "DELETE" or "PATCH")
                {
                    await FileLogger.WriteAsync("audit", structuredLine);
                }

                if (statusCode >= 400 || caughtException != null)
                {
                    var errorEntry = caughtException != null
                        ? FormatExceptionLog(context, requestTime, caughtException, elapsedMs)
                        : FormatErrorLog(context, requestTime, statusCode, elapsedMs);

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

            // 2. Registro estructurado en app-audit.log para panel de auditoría JSON
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
        // ── IP real considerando proxies (Render / Nginx / Cloudflare) ──────────
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        foreach (var header in new[] { "CF-Connecting-IP", "X-Real-IP", "X-Forwarded-For" })
        {
            if (ctx.Request.Headers.TryGetValue(header, out var hVal) && !string.IsNullOrWhiteSpace(hVal))
            {
                ip = hVal.ToString().Split(',')[0].Trim();
                break;
            }
        }

        var method  = ctx.Request.Method;
        var path    = ctx.Request.Path.Value ?? string.Empty;
        var qs      = RedactQueryString(path, ctx.Request.QueryString.Value ?? string.Empty);

        // ── Claims del usuario autenticado ────────────────────────────────────
        var user = ctx.User;
        string? userId    = null;
        string? userEmail = null;
        string? userRole  = null;
        string? userName  = null;

        if (user.Identity?.IsAuthenticated == true)
        {
            userId    = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? user.FindFirst("sub")?.Value
                        ?? user.Identity.Name;

            userEmail = user.FindFirst(ClaimTypes.Email)?.Value
                        ?? (user.Identity.Name?.Contains('@') == true ? user.Identity.Name : null);

            userRole  = user.FindFirst(ClaimTypes.Role)?.Value
                        ?? user.FindFirst("role")?.Value;

            userName  = user.FindFirst(ClaimTypes.GivenName)?.Value
                        ?? user.FindFirst("name")?.Value
                        ?? user.Identity.Name;
        }

        // ── Nivel de severidad ────────────────────────────────────────────────
        string level;
        if (statusCode >= 500 || ex != null)
            level = "ERROR";
        else if (statusCode >= 400)
            level = "WARNING";
        else if (method != "GET"
                 || path.Contains("/auth/", StringComparison.OrdinalIgnoreCase)
                 || path.Contains("/admin/", StringComparison.OrdinalIgnoreCase)
                 || path.Contains("/systemfiles/", StringComparison.OrdinalIgnoreCase)
                 || path.Contains("/privacy/", StringComparison.OrdinalIgnoreCase)
                 || path.Contains("/files/", StringComparison.OrdinalIgnoreCase)
                 || path.Contains("/teacher/students", StringComparison.OrdinalIgnoreCase)
                 || path.Contains("/teacher/submissions", StringComparison.OrdinalIgnoreCase))
            level = "AUDIT";
        else
            level = "INFO";

        // ── Acción descriptiva ────────────────────────────────────────────────
        var action = ResolveActionName(method, path, statusCode, qs);

        // ── Detalles de error ────────────────────────────────────────────────
        string? details = null;
        if (ex != null)
            details = $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        else if (statusCode >= 400)
            details = $"HTTP {statusCode} — {method} {path}{qs}";
        else if (!string.IsNullOrEmpty(qs) && level == "AUDIT")
            details = $"Parámetros: {qs}";

        return new AuditLogEntry
        {
            Timestamp = time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Level     = level,
            Method    = method,
            Path      = path + qs,
            StatusCode = statusCode,
            ElapsedMs = elapsedMs,
            Ip        = ip,
            UserId    = userId,
            UserEmail = userEmail,
            UserRole  = userRole,
            Action    = action,
            Details   = details
        };
    }

    /// <summary>
    /// Asigna una descripción legible en español a cada evento del sistema.
    /// Cubre: Auth, Admin, CRUD académico, asistencia, calificaciones,
    /// contenidos, perfil, sesión única, contraseña y errores de seguridad.
    /// </summary>
    private static string ResolveActionName(string method, string path, int status, string qs = "")
    {
        var p = path.ToLowerInvariant();

        // ════════════════════════════════════════════════════════════════════
        //  AUTENTICACIÓN
        // ════════════════════════════════════════════════════════════════════
        if (p.EndsWith("/auth/login"))
        {
            return status switch
            {
                200 => "Auth: Inicio de Sesión Exitoso",
                401 => "Auth: Contraseña o Usuario Incorrecto",
                403 => "Auth: Acceso Denegado en Login",
                404 => "Auth: Usuario No Encontrado",
                409 => "Auth: Alerta — Doble Sesión Activa Detectada",
                423 => "Auth: Cuenta Bloqueada por Demasiados Intentos Fallidos",
                429 => "Auth: Límite de Intentos de Login Excedido (Rate-Limit)",
                _   => "Auth: Intento de Inicio de Sesión Fallido"
            };
        }
        if (p.EndsWith("/auth/google-login") || p.EndsWith("/auth/google"))
            return status == 200 ? "Auth: Login Exitoso con Google OAuth" : "Auth: Fallo en Autenticación Google OAuth";

        if (p.EndsWith("/auth/force-login"))
            return "Auth: Cierre de Sesión Remota Forzado por Administrador";

        if (p.EndsWith("/auth/register"))
        {
            return status switch
            {
                201 => "Auth: Registro Exitoso de Nueva Cuenta Escolar",
                409 => "Auth: Intento de Registro con Email ya Existente",
                400 => "Auth: Datos de Registro Inválidos",
                _   => "Auth: Intento de Registro"
            };
        }
        if (p.EndsWith("/auth/logout"))
            return "Auth: Cierre de Sesión Voluntario";

        if (p.EndsWith("/auth/refresh") || p.EndsWith("/auth/refresh-token"))
            return status == 200 ? "Auth: Renovación de Token JWT Exitosa" : "Auth: Fallo al Renovar Token JWT";

        if (p.EndsWith("/auth/forgot-password") || p.EndsWith("/auth/reset-password"))
            return status == 200 ? "Auth: Solicitud de Restablecimiento de Contraseña" : "Auth: Fallo al Restablecer Contraseña";

        if (p.EndsWith("/auth/change-password"))
            return status == 200 ? "Auth: Cambio de Contraseña Exitoso" : "Auth: Intento Fallido de Cambio de Contraseña";

        if (p.EndsWith("/auth/verify-email") || p.EndsWith("/auth/confirm-email"))
            return "Auth: Verificación de Correo Electrónico";

        if (p.EndsWith("/auth/me") || p.EndsWith("/auth/profile"))
            return "Auth: Consulta de Perfil del Usuario Autenticado";

        if (p.Contains("/auth/"))
            return $"Auth: Operación de Autenticación [{method} {path}]";

        // ════════════════════════════════════════════════════════════════════
        //  ADMINISTRACIÓN
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/admin/backup-sql"))
            return status == 200 ? "Admin: Descarga de Respaldo SQL Generado" : "Admin: Error al Generar Respaldo SQL";

        if (p.Contains("/admin/restore-backup"))
            return status == 200 ? "Admin: Base de Datos Restaurada desde Respaldo" : "Admin: Error en Restauración de Base de Datos";

        if (p.Contains("/admin/audit-logs/download"))
            return "Admin: Descarga del Archivo Físico de Auditoría";

        if (p.Contains("/admin/audit-logs"))
            return "Admin: Consulta Paginada de Logs de Auditoría";

        if (p.Contains("/systemfiles/logs/clear"))
        {
            var fn = qs.Contains("fileName=") ? qs.Split("fileName=")[1].Split('&')[0] : "";
            return $"Admin: Archivo de Log Reiniciado — {fn}";
        }
        if (p.Contains("/systemfiles/logs/parsed"))
            return "Admin: Inspección Parseada de Archivo de Log";

        if (p.Contains("/systemfiles/logs/content"))
            return "Admin: Inspección de Contenido Raw de Archivo de Log";

        if (p.Contains("/systemfiles/logs"))
            return "Admin: Listado de Archivos de Log del Servidor";

        if (p.Contains("/systemfiles/htaccess"))
            return "Admin: Consulta de Configuración del Servidor (.htaccess)";

        if (p.Contains("/admin/stats") || p.Contains("/admin/dashboard"))
            return "Admin: Consulta de Estadísticas del Panel de Control";

        if (p.Contains("/admin/settings") || p.Contains("/admin/config"))
        {
            if (method == "PUT" || method == "PATCH") return "Admin: Actualización de Configuración del Sistema";
            return "Admin: Consulta de Configuración del Sistema";
        }

        // ════════════════════════════════════════════════════════════════════
        //  GESTIÓN DE USUARIOS (Admin / CRUD)
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/admin/users") || p.Contains("/usuarios"))
        {
            if (p.Contains("/reset-password") || p.Contains("/change-password"))
                return "Admin: Restablecimiento de Contraseña de Usuario";
            if (p.Contains("/toggle") || p.Contains("/activate") || p.Contains("/deactivate"))
                return "Admin: Cambio de Estado de Cuenta de Usuario";
            if (p.Contains("/role") || p.Contains("/rol"))
                return "Admin: Cambio de Rol de Usuario";
            return method switch
            {
                "POST"   => "Admin: Creación de Nuevo Usuario en el Sistema",
                "PUT"    => "Admin: Actualización de Datos de Usuario",
                "PATCH"  => "Admin: Modificación Parcial de Datos de Usuario",
                "DELETE" => "Admin: Eliminación / Baja de Usuario del Sistema",
                _        => "Admin: Consulta de Directorio de Usuarios"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  PERFIL DE USUARIO
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/profile") || p.Contains("/perfil"))
        {
            if (p.Contains("/avatar") || p.Contains("/photo") || p.Contains("/foto"))
                return method == "POST" || method == "PUT" ? "Usuario: Cambio de Foto de Perfil" : "Usuario: Consulta de Foto de Perfil";
            return method switch
            {
                "PUT"   => "Usuario: Actualización de Perfil Personal",
                "PATCH" => "Usuario: Modificación Parcial de Perfil Personal",
                _       => "Usuario: Consulta de Perfil Personal"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVACIDAD Y ACCESO A DATOS SENSIBLES DE MENORES (Ley 1581 / Ley 1098)
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/privacy/my-data"))
        {
            return method == "PUT"
                ? "Privacidad (Ley 1581): Rectificación de Datos Personales del Titular"
                : "Privacidad (Ley 1581): Exportación / Acceso a Datos Personales del Titular";
        }
        if (p.Contains("/privacy/consent") || p.Contains("/auth/accept-data-policy"))
            return "Privacidad (Ley 1581 / Ley 1098): Registro de Consentimiento Informado y Acudiente";
        if (p.Contains("/privacy/delete-account"))
            return "Privacidad (Ley 1581): Supresión y Anonimización de Cuenta";
        if (p.Contains("/teacher/students-status"))
            return "Auditoría (Ley 1098): Acceso a Estado de Semáforo y Actividad de Estudiantes Menores";
        if (p.Contains("/teacher/students"))
        {
            return method == "POST"
                ? "Auditoría (Ley 1098): Registro de Estudiante Menor por Docente"
                : "Auditoría (Ley 1098): Consulta de Datos Sensibles de Estudiantes Menores";
        }
        if (p.Contains("/files/submissions"))
            return "Auditoría (Ley 1098): Acceso/Descarga de Evidencia Escolar de Estudiante Menor";

        // ════════════════════════════════════════════════════════════════════
        //  CURSOS / MATERIAS
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/cursos") || p.Contains("/courses") || p.Contains("/materias"))
        {
            if (p.Contains("/enroll") || p.Contains("/inscribir") || p.Contains("/enrollment"))
                return method == "POST" ? "Académico: Inscripción de Estudiante en Curso" : "Académico: Consulta de Inscripciones";
            if (p.Contains("/unenroll") || p.Contains("/desinscribir"))
                return "Académico: Baja de Estudiante en Curso";
            if (p.Contains("/students") || p.Contains("/estudiantes"))
                return "Académico: Consulta de Estudiantes del Curso";
            return method switch
            {
                "POST"   => "Gestión Académica: Creación de Nueva Materia/Curso",
                "PUT"    => "Gestión Académica: Edición de Materia/Curso",
                "PATCH"  => "Gestión Académica: Modificación Parcial de Materia/Curso",
                "DELETE" => "Gestión Académica: Eliminación de Materia/Curso",
                _        => "Académico: Consulta de Cursos"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  ACTIVIDADES / TAREAS
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/actividades") || p.Contains("/tasks") || p.Contains("/assignments"))
        {
            if (p.Contains("/publish") || p.Contains("/publicar"))
                return "Docente: Publicación de Actividad/Tarea";
            return method switch
            {
                "POST"   => "Docente: Creación de Nueva Actividad/Tarea",
                "PUT"    => "Docente: Edición de Actividad/Tarea",
                "PATCH"  => "Docente: Modificación Parcial de Actividad",
                "DELETE" => "Docente: Eliminación de Actividad/Tarea",
                _        => "Académico: Consulta de Actividades/Tareas"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  ENTREGAS / SUBMISSIONS
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/entregas") || p.Contains("/submissions"))
        {
            if (p.Contains("/grade") || p.Contains("/calificar") || p.Contains("/review"))
                return method == "POST" || method == "PUT" ? "Docente: Calificación de Entrega" : "Docente: Revisión de Entregas";
            if (p.Contains("/file") || p.Contains("/archivo"))
                return method == "POST" ? "Estudiante: Subida de Archivo de Tarea" : "Académico: Consulta de Archivo de Entrega";
            return method switch
            {
                "POST"   => "Estudiante: Envío de Tarea / Entrega Académica",
                "PUT"    => "Estudiante: Actualización de Entrega Enviada",
                "DELETE" => "Estudiante: Retiro de Entrega Enviada",
                _        => "Académico: Consulta de Entregas"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  CALIFICACIONES / GRADES
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/calificaciones") || p.Contains("/grades") || p.Contains("/notas"))
        {
            if (p.Contains("/bulk") || p.Contains("/masivo"))
                return "Docente: Registro Masivo de Calificaciones";
            return method switch
            {
                "POST"   => "Docente: Registro de Calificación",
                "PUT"    => "Docente: Actualización de Calificación",
                "PATCH"  => "Docente: Ajuste Parcial de Calificación",
                "DELETE" => "Docente: Eliminación de Calificación",
                _        => "Académico: Consulta de Calificaciones"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  ASISTENCIA
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/asistencia") || p.Contains("/attendance"))
        {
            if (p.Contains("/report") || p.Contains("/reporte"))
                return "Docente/Admin: Reporte de Asistencia";
            if (p.Contains("/semaforo") || p.Contains("/traffic-light"))
                return "Sistema: Consulta de Semáforo de Asistencia";
            return method switch
            {
                "POST"   => "Docente: Registro de Asistencia de Clase",
                "PUT"    => "Docente: Corrección de Asistencia Registrada",
                "DELETE" => "Docente: Eliminación de Registro de Asistencia",
                _        => "Académico: Consulta de Asistencias"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONTENIDOS / MATERIALES
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/contenidos") || p.Contains("/contents") || p.Contains("/materials") || p.Contains("/recursos"))
        {
            if (p.Contains("/download") || p.Contains("/descargar"))
                return "Académico: Descarga de Material/Contenido";
            return method switch
            {
                "POST"   => "Docente: Subida/Creación de Material de Clase",
                "PUT"    => "Docente: Actualización de Material de Clase",
                "DELETE" => "Docente: Eliminación de Material de Clase",
                _        => "Académico: Consulta de Materiales/Contenidos"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  ANUNCIOS / NOTIFICATIONS
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/anuncios") || p.Contains("/announcements") || p.Contains("/notificaciones") || p.Contains("/notifications"))
        {
            return method switch
            {
                "POST"   => "Docente/Admin: Publicación de Anuncio/Notificación",
                "PUT"    => "Docente/Admin: Edición de Anuncio",
                "DELETE" => "Docente/Admin: Eliminación de Anuncio",
                _        => "Usuario: Consulta de Anuncios/Notificaciones"
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  REPORTES / ESTADÍSTICAS
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/reportes") || p.Contains("/reports") || p.Contains("/estadisticas") || p.Contains("/statistics"))
            return "Admin/Docente: Generación o Consulta de Reporte";

        // ════════════════════════════════════════════════════════════════════
        //  SESIÓN ÚNICA / SINGLE SESSION
        // ════════════════════════════════════════════════════════════════════
        if (p.Contains("/session") || p.Contains("/sesion") || p.Contains("/single-session"))
        {
            if (method == "DELETE") return "Admin: Cierre Forzado de Sesión Activa";
            return "Sistema: Validación de Sesión Única";
        }

        // ════════════════════════════════════════════════════════════════════
        //  ERRORES CONOCIDOS DE SEGURIDAD (sin autenticar en endpoints protegidos)
        // ════════════════════════════════════════════════════════════════════
        if (status == 401)
            return $"Seguridad: Acceso No Autenticado — {method} {path}";
        if (status == 403)
            return $"Seguridad: Acceso Denegado por Permisos Insuficientes — {method} {path}";
        if (status == 404 && method != "GET")
            return $"Operación sobre Recurso No Encontrado — {method} {path}";
        if (status == 429)
            return $"Seguridad: Límite de Solicitudes Excedido (Rate-Limit) — {method} {path}";
        if (status >= 500)
            return $"Error Interno del Servidor — {method} {path}";

        // ════════════════════════════════════════════════════════════════════
        //  GENÉRICO
        // ════════════════════════════════════════════════════════════════════
        return $"{method} {path}";
    }

    /// <summary>
    /// Formato estructurado obligatorio:
    /// [TIMESTAMP] | [NIVEL: AUDIT/INFO/ERROR] | [USER_ID / ROL] | [MÉTODO HTTP] | [RUTA/ENDPOINT] | [STATUS CODE] | [IP]
    /// </summary>
    private static string FormatStructuredLog(HttpContext ctx, DateTime time, int statusCode, Exception? ex)
    {
        var ip = ResolveClientIp(ctx);
        var method = ctx.Request.Method.ToUpperInvariant();
        var path = SafeRequestPath(ctx);
        var (userId, userRole) = ResolveUserIdentity(ctx);
        var level = DetermineLogLevel(method, path, statusCode, ex);
        var timestamp = time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return $"[{timestamp}] | [{level}] | [{userId} / {userRole}] | [{method}] | [{path}] | [{statusCode}] | [{ip}]";
    }

    /// <summary>
    /// Extrae automáticamente la identidad del usuario (Claims NameIdentifier / Role) desde HttpContext.User
    /// o decodificando el encabezado Authorization Bearer JWT.
    /// </summary>
    public static (string UserId, string Role) ResolveUserIdentity(HttpContext ctx)
    {
        string? userId = null;
        string? role = null;

        if (ctx.User?.Identity?.IsAuthenticated == true)
        {
            userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? ctx.User.FindFirst("user_id")?.Value
                     ?? ctx.User.FindFirst("sub")?.Value
                     ?? ctx.User.Identity.Name;

            role = ctx.User.FindFirst(ClaimTypes.Role)?.Value
                   ?? ctx.User.FindFirst("role")?.Value;
        }

        // Si no se extrajo de ctx.User (ej. token Bearer antes del middleware de autenticación), extraer del header Authorization
        if ((string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role)) &&
            ctx.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerVal = authHeader.ToString();
            if (headerVal.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var tokenStr = headerVal.Substring("Bearer ".Length).Trim();
                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(tokenStr))
                    {
                        var jwt = handler.ReadJwtToken(tokenStr);
                        userId ??= jwt.Claims.FirstOrDefault(c => c.Type is ClaimTypes.NameIdentifier or "sub" or "user_id")?.Value;
                        role ??= jwt.Claims.FirstOrDefault(c => c.Type is ClaimTypes.Role or "role")?.Value;
                    }
                }
                catch
                {
                    // Ignorar token no parseable
                }
            }
        }

        return (userId ?? "ANONYMOUS", role ?? "NO_ROLE");
    }

    private static string DetermineLogLevel(string method, string path, int statusCode, Exception? ex)
    {
        if (statusCode >= 500 || ex != null)
            return "ERROR";
        if (method is "POST" or "PUT" or "DELETE" or "PATCH"
            || path.Contains("/auth/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/admin/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/teacher/", StringComparison.OrdinalIgnoreCase))
            return "AUDIT";
        if (statusCode >= 400)
            return "WARNING";
        return "INFO";
    }

    private static string ResolveClientIp(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        foreach (var header in new[] { "CF-Connecting-IP", "X-Real-IP", "X-Forwarded-For" })
        {
            if (ctx.Request.Headers.TryGetValue(header, out var hVal) && !string.IsNullOrWhiteSpace(hVal))
            {
                ip = hVal.ToString().Split(',')[0].Trim();
                break;
            }
        }
        return ip;
    }

    private static string FormatErrorLog(HttpContext ctx, DateTime time, int statusCode, long elapsedMs)
    {
        var ip = ResolveClientIp(ctx);
        var method = ctx.Request.Method;
        var path = SafeRequestPath(ctx);
        var (userId, userRole) = ResolveUserIdentity(ctx);

        return $"[{time:yyyy-MM-dd HH:mm:ss}] | [ERROR] | [{userId} / {userRole}] | [{method}] | [{path}] | [{statusCode}] | [{ip}] ({elapsedMs}ms)";
    }

    private static string FormatExceptionLog(HttpContext ctx, DateTime time, Exception ex, long elapsedMs)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var method = ctx.Request.Method;
        var path = SafeRequestPath(ctx);

        return $"[{time:yyyy-MM-dd HH:mm:ss}] [EXCEPTION] {ip} | {method} {path} | {elapsedMs}ms | {ex.GetType().Name}: {ex.Message}\n    StackTrace: {ex.StackTrace}";
    }

    private static readonly Regex SensitiveQueryPattern = new(
        @"(?i)\b(token|access_token|refresh_token|idtoken|password|clave|contrasena|contraseña|secret|authorization|captcha|captchatoken)=([^&]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Evita registrar credenciales o identificadores sensibles en los logs.
    /// Los endpoints de autenticación no registran query string.
    /// </summary>
    private static string RedactQueryString(string path, string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return string.Empty;
        }

        if (path.Contains("/auth/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/privacy/", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var redacted = SensitiveQueryPattern.Replace(queryString, "$1=***");
        return Truncate(redacted, 300);
    }

    private static string SafeRequestPath(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        var qs = RedactQueryString(path, ctx.Request.QueryString.Value ?? string.Empty);
        return path + qs;
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= max ? value : value[..max];
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
    /// Escribir una línea en el archivo de log especificado con Flush() inmediato a disco/memoria.
    /// </summary>
    public static async Task WriteAsync(string channel, string message)
    {
        await _semaphore.WaitAsync();
        try
        {
            Directory.CreateDirectory(_logDirectory);
            var filePath = Path.Combine(_logDirectory, $"{channel}.log");
            RotateIfNeeded(filePath);

            await using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: true);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteLineAsync(message);
            await writer.FlushAsync();
            await stream.FlushAsync();
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
            innerException = ex.InnerException?.Message,
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
