using System.Security.Claims;
using System.Text.Json;
using LMS.API.Configuration;
using LMS.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LMS.API.Middleware;

/// <summary>
/// Valida que el JWT pertenezca a la sesión activa del usuario, que la cuenta siga
/// habilitada y que el titular haya aceptado la versión vigente de la política.
/// </summary>
public class SingleSessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SingleSessionMiddleware> _logger;
    private readonly PrivacyOptions _privacy;

    public SingleSessionMiddleware(
        RequestDelegate next,
        ILogger<SingleSessionMiddleware> logger,
        IOptions<PrivacyOptions> privacy)
    {
        _next = next;
        _logger = logger;
        _privacy = privacy.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = NormalizePath(context.Request.Path.Value);

        // Solo los endpoints realmente anónimos evitan la validación de sesión.
        if (IsAnonymousPath(path))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var username = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tokenSession = context.User.FindFirst("session_token")?.Value;

        if (string.IsNullOrWhiteSpace(username))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                "SESSION_INVALID", "La sesión no contiene un usuario válido.");
            return;
        }

        var dbContext = context.RequestServices.GetRequiredService<LMSDbContext>();
        var user = await dbContext.Usuarios
            .AsNoTracking()
            .Where(u => u.Username.ToLower() == username.ToLower())
            .Select(u => new
            {
                u.SessionToken,
                u.Activo,
                u.AnonymizedAt,
                u.DataPolicyAccepted,
                u.DataPolicyVersion
            })
            .FirstOrDefaultAsync();

        if (user == null || !user.Activo || user.AnonymizedAt != null)
        {
            _logger.LogWarning("Acceso rechazado para usuario inexistente, inactivo o anonimizado '{Username}'.", username);
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                "ACCOUNT_DISABLED", "La cuenta no está habilitada. Contacta a la institución.");
            return;
        }

        if (string.IsNullOrEmpty(user.SessionToken)
            || !string.Equals(user.SessionToken, tokenSession, StringComparison.Ordinal))
        {
            _logger.LogWarning("Sesión revocada o duplicada para usuario '{Username}'.", username);
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                "DUPLICATE_SESSION",
                "Tu sesión se ha cerrado porque se inició sesión en otro dispositivo o navegador.");
            return;
        }

        if (!IsPolicyExemptPath(path)
            && (!user.DataPolicyAccepted
                || !string.Equals(user.DataPolicyVersion, _privacy.PolicyVersion, StringComparison.Ordinal)))
        {
            _logger.LogWarning("Usuario '{Username}' sin aceptación vigente de la política {Version}.",
                username, _privacy.PolicyVersion);
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden,
                "POLICY_ACCEPTANCE_REQUIRED",
                "Debes aceptar la versión vigente de la política de tratamiento de datos.",
                new { currentVersion = _privacy.PolicyVersion });
            return;
        }

        await _next(context);
    }

    private static string NormalizePath(string? value)
    {
        var path = (value ?? string.Empty).Trim();
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }

    private static bool IsAnonymousPath(string path)
        => path.Equals("/api/health", StringComparison.OrdinalIgnoreCase)
           || path.StartsWith("/api/health/", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/ping", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/privacy/policy", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/auth/google-login", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase);

    private static bool IsPolicyExemptPath(string path)
        => path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/auth/validate-session", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/auth/accept-data-policy", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/privacy/policy", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/privacy/consent", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/privacy/my-data", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/api/privacy/delete-account", StringComparison.OrdinalIgnoreCase);

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        object? extra = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, private";

        var payload = new Dictionary<string, object?>
        {
            ["status"] = statusCode,
            ["code"] = code,
            ["message"] = message,
            ["timestamp"] = DateTime.UtcNow
        };

        if (extra is not null)
        {
            foreach (var property in extra.GetType().GetProperties())
            {
                payload[property.Name] = property.GetValue(extra);
            }
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

public static class SingleSessionMiddlewareExtensions
{
    public static IApplicationBuilder UseSingleSessionValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SingleSessionMiddleware>();
    }
}
