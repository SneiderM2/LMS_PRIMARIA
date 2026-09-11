namespace LMS.API.Middleware;

/// <summary>
/// Middleware que agrega cabeceras de seguridad HTTP a todas las respuestas.
/// Equivalente a las directivas mod_headers del .htaccess para Apache.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevenir clickjacking: solo permitir iframes del mismo origen
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

        // Prevenir MIME-sniffing del navegador
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Protección XSS en navegadores antiguos
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Política de referrer: enviar origen completo solo al mismo sitio
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Eliminar cabeceras que exponen información del servidor
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("Server");

        // Content Security Policy básica (ajustar según necesidades del frontend)
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "img-src 'self' data: blob:; " +
            "connect-src 'self' http://localhost:4200 http://localhost:5000;";

        // Prevenir que el sitio sea usado como recurso de terceros
        context.Response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=(), payment=()";

        await _next(context);
    }
}

/// <summary>
/// Extensión para registrar el middleware de forma limpia en Program.cs.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
