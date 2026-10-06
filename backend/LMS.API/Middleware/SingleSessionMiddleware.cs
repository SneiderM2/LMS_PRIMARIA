using System.Security.Claims;
using System.Text.Json;
using LMS.API.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Middleware;

public class SingleSessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SingleSessionMiddleware> _logger;

    public SingleSessionMiddleware(RequestDelegate next, ILogger<SingleSessionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, LMSDbContext dbContext)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Omitir la validación estricta de sesión única en los endpoints de autenticación inicial
        if (path.Contains("/api/auth/login") || 
            path.Contains("/api/auth/google-login") || 
            path.Contains("/api/auth/register") || 
            path.Contains("/api/auth/seed-users") ||
            path.Contains("/api/auth/me"))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var username = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tokenSession = context.User.FindFirst("session_token")?.Value;

            if (!string.IsNullOrEmpty(username))
            {
                var currentDbSessionToken = await dbContext.Usuarios
                    .AsNoTracking()
                    .Where(u => u.Username.ToLower() == username.ToLower())
                    .Select(u => u.SessionToken)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(currentDbSessionToken) && !string.Equals(currentDbSessionToken, tokenSession, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Sesión revocada o duplicada para usuario '{Username}'.", username);

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    var payload = new
                    {
                        status = 401,
                        code = "DUPLICATE_SESSION",
                        message = "Tu sesión se ha cerrado porque se inició sesión en otro dispositivo o navegador.",
                        timestamp = DateTime.UtcNow
                    };

                    await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
                    return;
                }
            }
        }

        await _next(context);
    }
}

public static class SingleSessionMiddlewareExtensions
{
    public static IApplicationBuilder UseSingleSessionValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SingleSessionMiddleware>();
    }
}