using System.Security.Claims;
using System.Text;
using LMS.API.Data;
using LMS.API.Middleware;
using LMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Controllers;

/// <summary>
/// Descarga autenticada y autorizada de adjuntos escolares.
/// Sustituye la exposición pública de /uploads (wwwroot) que permitía acceder a
/// tareas de menores con solo conocer la URL.
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly ILogger<FilesController> _logger;

    public FilesController(LMSDbContext context, IFileStorageService storage, ILogger<FilesController> logger)
    {
        _context = context;
        _storage = storage;
        _logger = logger;
    }

    [HttpGet("{**key}")]
    public async Task<IActionResult> Download(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound();
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var isAdmin = IsAdmin(currentUser.Rol?.Nombre);
        var normalizedKey = key.Replace('\\', '/').TrimStart('/');

        // Bloqueo explícito de path traversal antes de tocar el sistema de archivos.
        if (normalizedKey.Contains("..", StringComparison.Ordinal) || normalizedKey.Contains('\0'))
        {
            return BadRequest(new { message = "Ruta de archivo no válida." });
        }

        var isLegacy = normalizedKey.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase);

        StoredFileMetadata? metadata = null;
        string? physicalPath;
        string originalName;
        string contentType;

        if (isLegacy)
        {
            physicalPath = _storage.ResolveLegacyPhysicalPath(normalizedKey);
            originalName = FileSignatureValidator.SanitizeOriginalFileName(Path.GetFileName(normalizedKey));
            contentType = FileSignatureValidator.GetContentType(Path.GetExtension(normalizedKey));
        }
        else
        {
            metadata = await _storage.GetMetadataAsync(normalizedKey);
            physicalPath = _storage.ResolvePhysicalPath(normalizedKey);
            originalName = metadata?.OriginalName ?? FileSignatureValidator.SanitizeOriginalFileName(Path.GetFileName(normalizedKey));
            contentType = metadata?.ContentType ?? "application/octet-stream";
        }

        Stream? cloudStream = null;
        if (physicalPath == null || !System.IO.File.Exists(physicalPath))
        {
            cloudStream = await _storage.OpenReadStreamAsync(normalizedKey, HttpContext.RequestAborted);
            if (cloudStream == null)
            {
                return NotFound(new { message = "El archivo no existe o fue eliminado." });
            }
        }

        var authorized = await IsAuthorizedAsync(currentUser, normalizedKey, metadata, isLegacy, isAdmin);
        if (!authorized)
        {
            if (cloudStream != null) await cloudStream.DisposeAsync();
            _logger.LogWarning(
                "Acceso denegado a archivo {Key} por usuario {User}.",
                normalizedKey,
                currentUser.Username);

            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "No tienes autorización para descargar este archivo."
            });
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        var disposition = IsInlineContentType(contentType) ? "inline" : "attachment";
        Response.Headers.ContentDisposition = BuildContentDisposition(disposition, originalName);

        await WriteAuditAsync(currentUser, normalizedKey, isLegacy);

        if (physicalPath != null && System.IO.File.Exists(physicalPath))
        {
            return PhysicalFile(physicalPath, contentType, enableRangeProcessing: true);
        }

        return File(cloudStream!, contentType, enableRangeProcessing: true);
    }

    private async Task<bool> IsAuthorizedAsync(
        Entities.Usuario currentUser,
        string key,
        StoredFileMetadata? metadata,
        bool isLegacy,
        bool isAdmin)
    {
        if (isAdmin)
        {
            return true;
        }

        var urlVariants = new[]
        {
            "/api/files/" + key,
            "/" + key,
            key
        };

        var archivo = await _context.ArchivosEntrega
            .AsNoTracking()
            .Include(a => a.Entrega)
                .ThenInclude(e => e.Tarea)
                    .ThenInclude(t => t.Curso)
            .FirstOrDefaultAsync(a => urlVariants.Contains(a.RutaArchivo));

        if (archivo != null)
        {
            var ownerId = archivo.Entrega.AlumnoId;
            var teacherId = archivo.Entrega.Tarea.Curso.DocenteId;

            return currentUser.Id == ownerId || currentUser.Id == teacherId;
        }

        if (metadata != null)
        {
            return metadata.Scope switch
            {
                "resources" => true,
                "submissions" => metadata.OwnerUserId == currentUser.Id,
                _ => false
            };
        }

        // Archivo heredado sin registro asociado: solo administradores.
        return isLegacy && isAdmin;
    }

    private async Task<Entities.Usuario?> GetCurrentUserAsync()
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    private static bool IsAdmin(string? roleName)
        => roleName is not null
           && (roleName.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase)
               || roleName.Equals(".admin", StringComparison.OrdinalIgnoreCase)
               || roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));

    private static bool IsInlineContentType(string contentType)
        => contentType is "application/pdf" or "image/jpeg" or "image/png" or "image/webp";

    private static string BuildContentDisposition(string disposition, string fileName)
    {
        var safeName = FileSignatureValidator.SanitizeOriginalFileName(fileName);
        var ascii = new string(safeName.Select(c => c <= 127 ? c : '_').ToArray()).Replace("\"", string.Empty);
        var encoded = Uri.EscapeDataString(safeName);

        return $"{disposition}; filename=\"{ascii}\"; filename*=UTF-8''{encoded}";
    }

    private async Task WriteAuditAsync(Entities.Usuario user, string key, bool isLegacy)
    {
        try
        {
            await FileLogger.WriteAuditAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Level = "AUDIT",
                Method = HttpContext.Request.Method,
                Path = HttpContext.Request.Path + HttpContext.Request.QueryString,
                StatusCode = StatusCodes.Status200OK,
                ElapsedMs = 0,
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0",
                UserId = user.Username,
                UserEmail = user.Username.Contains('@') ? user.Username : null,
                UserRole = user.Rol?.Nombre,
                Action = "Privacidad: Descarga de archivo adjunto",
                Details = $"Archivo: {key} (heredado: {isLegacy})"
            });
        }
        catch
        {
            // La auditoría no debe bloquear la descarga.
        }
    }
}
