namespace LMS.API.Middleware;

/// <summary>
/// Cabeceras de seguridad para una API JSON/archivos.
/// La CSP del frontend (GitHub Pages) no se puede controlar desde aquí; este middleware
/// protege las respuestas de la API y evita que se incrusten en terceros.
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
        var headers = context.Response.Headers;
        var isFileDownload = context.Request.Path.StartsWithSegments("/api/files", StringComparison.OrdinalIgnoreCase);

        // El contenido de la API nunca debe renderizarse como documento.
        // En descargas se omite "sandbox" para no interferir con el visor de PDF del navegador.
        headers["Content-Security-Policy"] = isFileDownload
            ? "default-src 'none'; frame-ancestors 'none'; base-uri 'none'"
            : "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'; sandbox";
        headers["X-Frame-Options"] = "DENY";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] =
            "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), " +
            "fullscreen=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), " +
            "midi=(), payment=(), picture-in-picture=(), publickey-credentials-get=(), " +
            "screen-wake-lock=(), sync-xhr=(), usb=(), xr-spatial-tracking=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-site";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        headers["X-Download-Options"] = "noopen";

        // Refuerzo heredado para navegadores antiguos: desactivado a propósito.
        headers["X-XSS-Protection"] = "0";

        headers.Remove("X-Powered-By");
        headers.Remove("Server");

        // Respuestas con datos personales no deben quedar en cachés intermedias.
        if (IsSensitivePath(context.Request.Path))
        {
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private";
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "0";
        }

        await _next(context);
    }

    private static bool IsSensitivePath(PathString path)
    {
        return path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/privacy", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/files", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/student", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/teacher", StringComparison.OrdinalIgnoreCase);
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
