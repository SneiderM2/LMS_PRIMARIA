using System.Security.Claims;
using System.Text.Json;
using LMS.API.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Middleware;

/// <summary>
/// Middleware de control de sesión única.
/// Valida en cada petición autenticada que el session_token embebido en los Claims del JWT
/// coincida con el session_token registrado actualmente en la base de datos de Supabase.
/// Si no coincide (porque se inició sesión en otro dispositivo o navegador),
/// invalida la petición respondiendo HTTP 401 Unauthorized de forma explícita.
/// </summary>
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
        // Solo evaluar si la petición ya fue autenticada con JWT
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var username = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tokenSession = context.User.FindFirst("session_token")?.Value;

            if (!string.IsNullOrEmpty(username))
            {
                // Consultar el session_token persistido actualmente en la base de datos
                var currentDbSessionToken = await dbContext.Usuarios
                    .AsNoTracking()
                    .Where(u => u.Username.ToLower() == username.ToLower())
                    .Select(u => u.SessionToken)
                    .FirstOrDefaultAsync();

                // Si en BD existe un session_token y es distinto al que porta el JWT entrante (o este no tiene session_token)
                if (!string.IsNullOrEmpty(currentDbSessionToken) && !string.Equals(currentDbSessionToken, tokenSession, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Sesión revocada o duplicada para usuario '{Username}'. El session_token del JWT ('{JwtToken}') no coincide con el registrado en Supabase ('{DbToken}').", 
                        username, tokenSession ?? "NULO", currentDbSessionToken);

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
    /// <summary>
    /// Registra el middleware de verificación de sesión única en el pipeline HTTP de ASP.NET Core.
    /// Debe ubicarse inmediatamente después de app.UseAuthentication() y antes de app.UseAuthorization().
    /// </summary>
    public static IApplicationBuilder UseSingleSessionValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SingleSessionMiddleware>();
    }
}
