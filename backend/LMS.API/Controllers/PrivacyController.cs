using System.Security.Claims;
using System.Text;
using System.Text.Json;
using LMS.API.Configuration;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Middleware;
using LMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LMS.API.Controllers;

/// <summary>
/// Ejercicio de los derechos del titular y transparencia del tratamiento
/// (Ley 1581 de 2012, art. 8; Decreto 1074 de 2015; lineamientos de la SIC).
/// </summary>
[ApiController]
[Route("api/privacy")]
public class PrivacyController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly ConsentService _consent;
    private readonly IFileStorageService _storage;
    private readonly PrivacyOptions _privacy;
    private readonly ILogger<PrivacyController> _logger;

    public PrivacyController(
        LMSDbContext context,
        ConsentService consent,
        IFileStorageService storage,
        IOptions<PrivacyOptions> privacy,
        ILogger<PrivacyController> logger)
    {
        _context = context;
        _consent = consent;
        _storage = storage;
        _privacy = privacy.Value;
        _logger = logger;
    }

    /// <summary>
    /// Aviso de privacidad y metadatos de la política vigente (público).
    /// </summary>
    [HttpGet("policy")]
    [AllowAnonymous]
    public IActionResult GetPolicy()
    {
        return Ok(new
        {
            version = _privacy.PolicyVersion,
            effectiveDate = _privacy.PolicyEffectiveDate,
            policyUrl = _privacy.PolicyUrl,
            controller = new
            {
                name = _privacy.ControllerName,
                nit = _privacy.ControllerNit,
                address = _privacy.ControllerAddress,
                email = _privacy.ContactEmail,
                phone = _privacy.ContactPhone,
                dpo = _privacy.DpoName
            },
            purposes = new[]
            {
                "Gestión académica: matrícula, cursos, tareas, calificaciones y certificados.",
                "Comunicación institucional con el estudiante y su acudiente.",
                "Seguridad de la información, auditoría y prevención de fraude.",
                "Cumplimiento de obligaciones legales del sector educativo."
            },
            rights = new[]
            {
                new { code = "conocer", title = "Conocer", description = "Conocer los datos personales que reposan en la plataforma." },
                new { code = "actualizar", title = "Actualizar y rectificar", description = "Solicitar la corrección de datos inexactos o incompletos." },
                new { code = "suprimir", title = "Suprimir", description = "Solicitar la supresión cuando no exista deber legal o contractual de conservarlos." },
                new { code = "revocar", title = "Revocar la autorización", description = "Revocar el consentimiento para finalidades no esenciales." },
                new { code = "queja", title = "Presentar queja", description = "Acudir a la Superintendencia de Industria y Comercio (SIC) o al Delegado de Protección de Datos." },
                new { code = "portabilidad", title = "Portabilidad", description = "Recibir una copia estructurada de los datos entregados a la plataforma." }
            },
            retention = new
            {
                academicYears = _privacy.AcademicRetentionYears,
                logDays = _privacy.LogRetentionDays,
                inactivityAnonymizationMonths = _privacy.InactivityAnonymizationMonths
            },
            internationalTransfers = new[]
            {
                new { provider = "Proveedor de almacenamiento de datos en la nube", country = "Estados Unidos", purpose = "Almacenamiento seguro de la información", safeguard = "Cláusulas contractuales y cifrado en tránsito" },
                new { provider = "Proveedor de alojamiento de la plataforma", country = "Estados Unidos", purpose = "Funcionamiento de los servicios de la plataforma", safeguard = "Cláusulas contractuales y cifrado en tránsito" },
                new { provider = "Proveedor de publicación del sitio web", country = "Estados Unidos", purpose = "Publicación del sitio web", safeguard = "Sitio estático sin datos personales" }
            },
            children = "El tratamiento de datos de menores de edad requiere la autorización previa, expresa e informada de su representante legal, conforme al artículo 7 de la Ley 1581 de 2012 y la Ley 1098 de 2006.",
            authority = "Superintendencia de Industria y Comercio — " + _privacy.SuperintendenceUrl
        });
    }

    /// <summary>
    /// Registra la aceptación de la política con evidencia verificable (versión, fecha, IP, agente, acudiente).
    /// </summary>
    [HttpPost("consent")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterConsent([FromBody] ConsentRequestDto dto)
    {
        if (!dto.Accepted)
        {
            return BadRequest(new { message = "Debes aceptar la política de tratamiento de datos para continuar." });
        }

        if (!string.Equals(dto.Version, _privacy.PolicyVersion, StringComparison.Ordinal))
        {
            return Conflict(new
            {
                message = "La versión de la política cambió. Recarga la página y vuelve a leerla.",
                currentVersion = _privacy.PolicyVersion
            });
        }

        var user = await GetCurrentUserAsync(includeStudent: true);
        if (user == null)
        {
            return Unauthorized();
        }

        var isStudent = user.Alumno != null
                        || string.Equals(user.Rol?.Nombre, "ALUMNO", StringComparison.OrdinalIgnoreCase);

        if (_privacy.RequireGuardianConsentForMinors && isStudent)
        {
            var guardianValidation = ConsentService.ValidateGuardianConsent(
                dto.GuardianAuthorization,
                dto.GuardianFullName,
                dto.GuardianDocument);

            if (!guardianValidation.IsValid)
            {
                return BadRequest(new
                {
                    code = "GUARDIAN_CONSENT_REQUIRED",
                    message = guardianValidation.Message
                });
            }
        }

        user.DataPolicyAccepted = true;
        user.DataPolicyAcceptedAt = DateTime.UtcNow;
        user.DataPolicyVersion = _privacy.PolicyVersion;
        user.DataPolicyEvidence = _consent.BuildEvidence(
            user,
            "login-modal",
            ClientIp,
            Request.Headers.UserAgent.ToString(),
            dto.GuardianFullName,
            dto.GuardianDocument);

        await _context.SaveChangesAsync();

        await AuditAsync(user, "Privacidad: Aceptación de política de tratamiento de datos",
            $"Versión {_privacy.PolicyVersion}; evidencia firmada con HMAC-SHA256.");

        return Ok(new
        {
            message = "Política de tratamiento de datos aceptada y registrada.",
            acceptedAt = user.DataPolicyAcceptedAt,
            version = user.DataPolicyVersion
        });
    }

    /// <summary>
    /// Derecho de acceso y portabilidad: entrega una copia estructurada de los datos del titular.
    /// </summary>
    [HttpGet("my-data")]
    [Authorize]
    public async Task<IActionResult> ExportMyData()
    {
        var user = await GetCurrentUserAsync(includeStudent: true);
        if (user == null)
        {
            return Unauthorized();
        }

        var matriculas = new List<object>();
        if (user.Alumno != null)
        {
            var rows = await _context.Inscripciones
                .AsNoTracking()
                .Where(i => i.AlumnoId == user.Id)
                .OrderBy(i => i.FechaRegistro)
                .Select(i => new
                {
                    curso = i.Curso.Nombre,
                    grado = i.Curso.Grado.Nombre,
                    grupo = i.Curso.Grupo,
                    docente = i.Curso.Docente.Nombre + " " + i.Curso.Docente.Apellido,
                    estado = i.Estado,
                    fechaRegistro = i.FechaRegistro
                })
                .ToListAsync();

            matriculas.AddRange(rows);
        }

        var entregas = await _context.Entregas
            .AsNoTracking()
            .Where(e => e.AlumnoId == user.Id)
            .OrderByDescending(e => e.FechaEntrega)
            .Select(e => new
            {
                tarea = e.Tarea.Titulo,
                curso = e.Tarea.Curso.Nombre,
                e.FechaEntrega,
                e.Estado,
                e.Calificacion,
                e.Retroalimentacion,
                archivos = e.Archivos.Select(a => new
                {
                    a.NombreOriginal,
                    a.TipoMime,
                    a.TamanoBytes,
                    a.FechaSubida
                }).ToList()
            })
            .ToListAsync();

        var payload = new
        {
            generatedAt = DateTime.UtcNow,
            formatVersion = "1.0",
            titular = new
            {
                idInterno = user.Id,
                usuario = user.Username,
                nombre = user.Nombre,
                apellido = user.Apellido,
                rol = user.Rol?.Nombre,
                grado = user.Alumno?.Grado?.Nombre,
                activo = user.Activo,
                fechaCreacion = user.FechaCreacion,
                ultimoAcceso = user.LastLoginAt,
                politica = new
                {
                    aceptada = user.DataPolicyAccepted,
                    version = user.DataPolicyVersion,
                    fecha = user.DataPolicyAcceptedAt,
                    evidencia = user.DataPolicyEvidence
                }
            },
            matriculas,
            entregas,
            nota = "Copia de datos personales generada automáticamente. No incluye contraseñas, tokens de sesión ni información de seguridad."
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await AuditAsync(user, "Privacidad: Exportación de datos del titular (acceso/portabilidad)",
            $"Registros exportados: {matriculas.Count} matrículas, {entregas.Count} entregas.");

        Response.Headers.CacheControl = "private, no-store";

        return File(
            Encoding.UTF8.GetBytes(json),
            "application/json; charset=utf-8",
            $"mis-datos-lms-{DateTime.UtcNow:yyyyMMdd-HHmm}.json");
    }

    /// <summary>
    /// Derecho de rectificación de datos propios (nombres y apellidos).
    /// El cambio de documento/carnet requiere verificación presencial ante la institución.
    /// </summary>
    [HttpPut("my-data")]
    [Authorize]
    public async Task<IActionResult> RectifyMyData([FromBody] RectifyPersonalDataDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await GetCurrentUserAsync(includeStudent: false);
        if (user == null)
        {
            return Unauthorized();
        }

        var previousName = user.FullName;
        user.Nombre = dto.Nombre.Trim();
        user.Apellido = dto.Apellido.Trim();
        await _context.SaveChangesAsync();

        await AuditAsync(user, "Privacidad: Rectificación de datos personales",
            $"Nombre anterior: {previousName}");

        return Ok(new
        {
            message = "Tus datos fueron actualizados. Si necesitas cambiar tu documento o carnet, contacta a la institución.",
            fullName = user.FullName
        });
    }

    /// <summary>
    /// Derecho de supresión: anonimiza la cuenta y elimina los archivos personales,
    /// conservando únicamente la historia académica exigida por la institución.
    /// </summary>
    [HttpPost("delete-account")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!string.Equals(dto.Confirmation?.Trim(), "ELIMINAR MI CUENTA", StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Escribe exactamente: ELIMINAR MI CUENTA" });
        }

        var user = await GetCurrentUserAsync(includeStudent: true);
        if (user == null)
        {
            return Unauthorized();
        }

        if (IsAdmin(user.Rol?.Nombre))
        {
            return BadRequest(new { message = "Las cuentas administrativas no pueden eliminarse desde este formulario. Contacta al responsable del tratamiento." });
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return BadRequest(new { message = "La contraseña no es correcta." });
        }

        var archivos = await _context.ArchivosEntrega
            .Include(a => a.Entrega)
            .Where(a => a.Entrega.AlumnoId == user.Id)
            .ToListAsync();

        foreach (var archivo in archivos)
        {
            await _storage.DeleteAsync(archivo.RutaArchivo);
        }

        _context.ArchivosEntrega.RemoveRange(archivos);

        // La historia académica se conserva, pero se elimina el contenido libre del titular.
        var entregas = await _context.Entregas.Where(e => e.AlumnoId == user.Id).ToListAsync();
        foreach (var entrega in entregas)
        {
            entrega.ContenidoTexto = null;
        }

        user.Username = $"suprimido-{user.Id}-{Guid.NewGuid():N}";
        user.Nombre = "Titular";
        user.Apellido = "Suprimido";
        user.AvatarUrl = null;
        user.Activo = false;
        user.SessionToken = null;
        user.LockoutEnd = null;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), workFactor: 12);
        user.DeletedAt = DateTime.UtcNow;
        user.AnonymizedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AuditAsync(user, "Privacidad: Supresión y anonimización de cuenta",
            $"Archivos eliminados: {archivos.Count}. Motivo declarado: {Truncate(dto.Reason, 200) ?? "no indicado"}.");

        return Ok(new
        {
            message = "Tu cuenta fue anonimizada y tus archivos personales fueron eliminados. La historia académica se conserva por deber legal de la institución.",
            anonymizedAt = user.AnonymizedAt,
            retention = $"Historia académica: {_privacy.AcademicRetentionYears} años."
        });
    }

    private async Task<Usuario?> GetCurrentUserAsync(bool includeStudent)
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var query = _context.Usuarios
            .Include(u => u.Rol)
            .AsQueryable();

        if (includeStudent)
        {
            query = query
                .Include(u => u.Alumno)
                    .ThenInclude(a => a!.Grado);
        }

        return await query.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private static bool IsAdmin(string? roleName)
        => roleName is not null
           && (roleName.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase)
               || roleName.Equals(".admin", StringComparison.OrdinalIgnoreCase)
               || roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= max ? value : value[..max];
    }

    private async Task AuditAsync(Usuario user, string action, string? details)
    {
        try
        {
            await FileLogger.WriteAuditAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Level = "AUDIT",
                Method = HttpContext.Request.Method,
                Path = HttpContext.Request.Path.ToString(),
                StatusCode = StatusCodes.Status200OK,
                ElapsedMs = 0,
                Ip = ClientIp ?? "0.0.0.0",
                UserId = user.Username,
                UserEmail = user.Username.Contains('@') ? user.Username : null,
                UserRole = user.Rol?.Nombre,
                Action = action,
                Details = details
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible escribir la auditoría de privacidad.");
        }
    }
}
