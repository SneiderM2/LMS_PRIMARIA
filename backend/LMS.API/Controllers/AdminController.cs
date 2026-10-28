using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Middleware;
using LMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAdmin")]
public class AdminController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public AdminController(
        LMSDbContext context,
        ILogger<AdminController> logger,
        IWebHostEnvironment env,
        IConfiguration configuration,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _env = env;
        _configuration = configuration;
        _cache = cache;
    }

    private void InvalidateAdminCache()
    {
        _cache.Remove("admin_metrics");
        _cache.Remove("admin_activity_stats");
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(AdminMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminMetrics()
    {
        if (_cache.TryGetValue("admin_metrics", out AdminMetricsDto? cached) && cached != null)
        {
            return Ok(cached);
        }

        var totalActive = await _context.Usuarios.AsNoTracking().CountAsync(u => u.Activo);
        var activeTeachers = await _context.Usuarios.AsNoTracking().CountAsync(u => u.Activo && u.Rol.Nombre == "DOCENTE");
        var activeStudents = await _context.Usuarios.AsNoTracking().CountAsync(u => u.Activo && u.Rol.Nombre == "ALUMNO");
        var totalCourses = await _context.Cursos.AsNoTracking().CountAsync(c => c.Activo);

        string systemStatus = "Operativo";
        try
        {
            if (!await _context.Database.CanConnectAsync()) systemStatus = "Degradado (Almacenamiento no disponible)";
        }
        catch
        {
            systemStatus = "Degradado (Almacenamiento no disponible)";
        }

        int recentErrors = 0;
        try
        {
            var logsDir = FileLogger.LogDirectory;
            if (!Directory.Exists(logsDir)) logsDir = Path.Combine(AppContext.BaseDirectory, "logs");

            var errorLogPath = Path.Combine(logsDir, "error.log");
            if (System.IO.File.Exists(errorLogPath))
            {
                var todayPrefix = DateTime.Now.ToString("yyyy-MM-dd");
                using var fs = new FileStream(errorLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("[" + todayPrefix) && (line.Contains("[HTTP-") || line.Contains("[EXCEPTION]")))
                    {
                        recentErrors++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al calcular métricas de error.log");
        }

        var metrics = new AdminMetricsDto
        {
            TotalActiveUsers = totalActive,
            ActiveTeachersCount = activeTeachers,
            ActiveStudentsCount = activeStudents,
            SystemStatus = systemStatus,
            RecentErrorsCount = recentErrors,
            TotalCoursesCount = totalCourses
        };

        _cache.Set("admin_metrics", metrics, TimeSpan.FromSeconds(30));
        return Ok(metrics);
    }

    /// <summary>
    /// Consulta el directorio de usuarios (Alumnos, Docentes y Administradores).
    /// GET: /api/admin/users?role=&search=
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(List<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? role = null,
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        var query = _context.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
            .Where(u => u.AnonymizedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(role) && !role.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var roleUpper = role.ToUpperInvariant();
            if (roleUpper == "STUDENT" || roleUpper == "ALUMNO")
                query = query.Where(u => u.Rol.Nombre == "ALUMNO");
            else if (roleUpper == "TEACHER" || roleUpper == "DOCENTE")
                query = query.Where(u => u.Rol.Nombre == "DOCENTE");
            else if (roleUpper.Contains("ADMIN"))
                query = query.Where(AdminRolePredicate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(s) ||
                                     u.Nombre.ToLower().Contains(s) ||
                                     u.Apellido.ToLower().Contains(s));
        }

        var users = await query
            .OrderBy(u => u.Rol.Nombre.ToLower().Contains("admin") ? 1
                        : (u.Rol.Nombre == "DOCENTE" ? 2
                        : (u.Rol.Nombre == "ALUMNO" ? 3 : 4)))
            .ThenBy(u => (u.Rol.Nombre == "ALUMNO" && (u.Alumno == null || u.Alumno.Grado == null || string.IsNullOrEmpty(u.Alumno.Grado.Nombre))) ? 1 : 0)
            .ThenBy(u => (u.Alumno != null && u.Alumno.Grado != null) ? u.Alumno.Grado.Nombre : string.Empty)
            .ThenBy(u => u.Nombre)
            .ThenBy(u => u.Apellido)
            .ThenByDescending(u => u.Id)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.Username,
                Nombre = u.Nombre,
                Apellido = u.Apellido,
                Role = u.Rol.Nombre == "ALUMNO" ? "Student" : (u.Rol.Nombre == "DOCENTE" ? "Teacher" : "Admin"),
                GradeLevel = (u.Alumno != null && u.Alumno.Grado != null) ? u.Alumno.Grado.Nombre : null,
                Activo = u.Activo,
                AvatarUrl = u.AvatarUrl,
                FechaCreacion = u.FechaCreacion
            })
            .ToListAsync();

        Response.Headers["X-Total-Count"] = users.Count.ToString();

        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            "Directorio de Usuarios",
            $"Consulta de directorio ({users.Count} cuentas, rol='{role ?? "ALL"}', búsqueda='{search ?? "N/A"}')");

        await FileLogger.LogAsync("AUDIT",
            $"[Ley 1581 / Ley 1098] Consulta administrativa de directorio de usuarios y datos de menores ({users.Count} registros, rol='{role ?? "ALL"}', búsqueda='{search ?? ""}') por {User.Identity?.Name ?? "Admin"}.");

        if (page.HasValue && pageSize.HasValue && page.Value > 0 && pageSize.Value > 0)
        {
            var paged = users.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToList();
            return Ok(paged);
        }

        return Ok(users);
    }

    /// <summary>
    /// Registra una nueva cuenta de usuario (Docente, Estudiante o Administrador).
    /// POST: /api/admin/users
    /// </summary>
    [HttpPost("users")]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateAdminUserDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cleanUsername = dto.Username.Trim();
        if (await _context.Usuarios.AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower()))
        {
            return BadRequest(new { message = "El carnet / documento ya existe en el sistema." });
        }

        var targetRoleName = "ALUMNO";
        var r = dto.Role.ToUpperInvariant();
        if (r.Contains("DOCENTE") || r.Contains("TEACHER")) targetRoleName = "DOCENTE";
        else if (r.Contains("ADMIN")) targetRoleName = "ADMINISTRADOR";

        var rol = await _context.Roles.FirstOrDefaultAsync(ro => ro.Nombre == targetRoleName)
                  ?? await _context.Roles.FirstOrDefaultAsync();

        if (rol == null) return BadRequest(new { message = "No se encontró el rol en el sistema." });

        Grado? grado = null;
        string? gradeResult = null;
        if (targetRoleName == "ALUMNO")
        {
            var rawGrade = dto.GradeLevel?.Trim() ?? "1°";
            var gradeKey = rawGrade.Contains('°') ? rawGrade.Substring(0, rawGrade.IndexOf('°') + 1) : rawGrade;
            var grados = await _context.Grados.ToListAsync();
            grado = grados.FirstOrDefault(g => g.Nombre == gradeKey || g.Nombre == rawGrade);

            if (grado == null)
            {
                return BadRequest(new
                {
                    code = "INVALID_GRADE",
                    message = $"El grado '{rawGrade}' no existe. Selecciona uno de los grados registrados.",
                    validGrades = grados.Select(g => g.Nombre).OrderBy(g => g).ToArray()
                });
            }

            gradeResult = grado.Nombre;
        }

        var password = dto.Password?.Trim();
        var generatedPassword = false;

        if (string.IsNullOrWhiteSpace(password))
        {
            password = PasswordPolicy.GenerateTemporaryPassword();
            generatedPassword = true;
        }
        else
        {
            var passwordCheck = PasswordPolicy.Validate(password, cleanUsername, $"{dto.Nombre} {dto.Apellido}");
            if (!passwordCheck.IsValid)
            {
                return BadRequest(new { code = "WEAK_PASSWORD", message = passwordCheck.Message });
            }
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        var avatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(cleanUsername)}";

        var usuario = new Usuario
        {
            RolId = rol.Id,
            Username = cleanUsername,
            PasswordHash = passwordHash,
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            AvatarUrl = avatarUrl,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            // Las cuentas creadas por administración no tienen consentimiento presunto.
            // El titular (o su acudiente) debe aceptar la política en el primer ingreso.
            DataPolicyAccepted = false,
            DataPolicyAcceptedAt = null,
            DataPolicyVersion = null
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
        InvalidateAdminCache();

        if (targetRoleName == "ALUMNO" && grado != null)
        {
            _context.Alumnos.Add(new Alumno { UsuarioId = usuario.Id, GradoId = grado.Id });
            await _context.SaveChangesAsync();
        }

        var result = new AdminUserDto
        {
            Id = usuario.Id,
            Username = usuario.Username,
            FullName = usuario.FullName,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Role = targetRoleName == "ALUMNO" ? "Student" : (targetRoleName == "DOCENTE" ? "Teacher" : "Admin"),
            GradeLevel = gradeResult,
            Activo = usuario.Activo,
            AvatarUrl = usuario.AvatarUrl,
            FechaCreacion = usuario.FechaCreacion,
            TemporaryPassword = generatedPassword ? password : null
        };

        _logger.LogInformation("[AUDIT] El Administrador {Admin} creó el usuario {Username} con rol {Role}",
            User.Identity?.Name ?? "Admin", usuario.Username, targetRoleName);
        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            $"Alta de Usuario ({targetRoleName})",
            $"Cuenta '{usuario.Username}' ({usuario.FullName}) creada con rol {targetRoleName}" + (gradeResult != null ? $" en Grado {gradeResult}" : ""));
        await FileLogger.LogAsync("AUDIT", $"El Administrador {User.Identity?.Name ?? "Admin"} creó el usuario {usuario.Username} ({usuario.FullName}) con rol {targetRoleName}");

        return CreatedAtAction(nameof(GetUsers), new { id = usuario.Id }, result);
    }

    /// <summary>
    /// Actualiza los datos de un usuario escolar existente.
    /// PUT: /api/admin/users/{id}
    /// </summary>
    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateAdminUserDto dto)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.Nombre)) usuario.Nombre = dto.Nombre.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Apellido)) usuario.Apellido = dto.Apellido.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            var passwordCheck = PasswordPolicy.Validate(dto.Password, usuario.Username, usuario.FullName);
            if (!passwordCheck.IsValid)
            {
                return BadRequest(new { code = "WEAK_PASSWORD", message = passwordCheck.Message });
            }

            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12);

            // Al cambiar la contraseña se invalidan las sesiones activas del usuario.
            usuario.SessionToken = null;
            usuario.AccessFailedCount = 0;
            usuario.LockoutEnd = null;
        }

        if (!string.IsNullOrWhiteSpace(dto.Role))
        {
            var roleName = dto.Role.Trim().ToUpperInvariant();
            var targetRoleName = roleName.Contains("ADMIN")
                ? "ADMINISTRADOR"
                : roleName.Contains("DOCENTE") || roleName.Contains("TEACHER")
                    ? "DOCENTE"
                    : "ALUMNO";

            var targetRole = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == targetRoleName);
            if (targetRole == null)
            {
                return BadRequest(new { message = "El rol solicitado no existe." });
            }

            var currentlyAdmin = IsAdminRole(usuario.Rol?.Nombre);
            if (currentlyAdmin && targetRoleName != "ADMINISTRADOR")
            {
                var activeAdmins = await _context.Usuarios
                    .Where(AdminRolePredicate)
                    .CountAsync(u => u.Activo);

                if (activeAdmins <= 1)
                {
                    return BadRequest(new { message = "No es posible retirar el rol al último administrador activo." });
                }
            }

            if (usuario.RolId != targetRole.Id)
            {
                usuario.RolId = targetRole.Id;

                // El rol viaja en el JWT; se invalida la sesión para que el cambio
                // de privilegios surta efecto de inmediato y no espere 8 horas.
                usuario.SessionToken = null;
            }
        }

        // Actualizar grado de alumno si aplica
        if (usuario.Alumno != null && !string.IsNullOrWhiteSpace(dto.GradeLevel))
        {
            var rawGrade = dto.GradeLevel.Trim();
            var gradeKey = rawGrade.Contains('°') ? rawGrade[..(rawGrade.IndexOf('°') + 1)] : rawGrade;
            var grados = await _context.Grados.ToListAsync();
            var grado = grados.FirstOrDefault(g => g.Nombre == gradeKey || g.Nombre == rawGrade);

            if (grado == null)
            {
                return BadRequest(new
                {
                    code = "INVALID_GRADE",
                    message = $"El grado '{rawGrade}' no existe. Selecciona uno de los grados registrados.",
                    validGrades = grados.Select(g => g.Nombre).OrderBy(g => g).ToArray()
                });
            }

            usuario.Alumno.GradoId = grado.Id;
        }

        await _context.SaveChangesAsync();
        InvalidateAdminCache();
        _logger.LogInformation("Usuario ID {Id} actualizado por administrador {Admin}", id, User.Identity?.Name);
        await AuditAsync("Administración: Actualización de usuario",
            $"Usuario {usuario.Username} (ID {usuario.Id}) actualizado. Cambio de contraseña: {!string.IsNullOrWhiteSpace(dto.Password)}.");

        return Ok(new { message = "Usuario actualizado exitosamente." });
    }

    /// <summary>
    /// Suspende / activa el acceso a un usuario sin eliminar sus datos históricos (Soft Delete).
    /// PATCH: /api/admin/users/{id}/toggle-status
    /// </summary>
    [HttpPatch("users/{id:int}/toggle-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleUserStatus(int id)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

        // No permitir que el admin se inactive a sí mismo accidentalmente.
        // NameIdentifier contiene el username; User.Identity.Name contiene el nombre completo.
        var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (usuario.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "No puedes suspender tu propia cuenta de administrador en uso." });
        }

        // Tampoco se debe dejar el sistema sin ningún administrador activo.
        if (usuario.Activo && IsAdminRole(usuario.Rol?.Nombre))
        {
            var activeAdmins = await _context.Usuarios
                .Where(AdminRolePredicate)
                .CountAsync(u => u.Activo);

            if (activeAdmins <= 1)
            {
                return BadRequest(new { message = "No es posible suspender al último administrador activo." });
            }
        }

        usuario.Activo = !usuario.Activo;
        if (!usuario.Activo)
        {
            // Invalida inmediatamente el JWT del usuario suspendido.
            usuario.SessionToken = null;
        }

        await _context.SaveChangesAsync();
        InvalidateAdminCache();

        var statusText = usuario.Activo ? "habilitada" : "suspendida";
        _logger.LogInformation("[AUDIT] El Administrador {Admin} cambió el estado de la cuenta {Username} a {StatusText}",
            User.Identity?.Name ?? "Admin", usuario.Username, statusText);
        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            $"Estado de Cuenta ({statusText.ToUpperInvariant()})",
            $"Usuario '{usuario.Username}' ({usuario.FullName}) {statusText} por {User.Identity?.Name ?? "Admin"}");
        await FileLogger.LogAsync("AUDIT", $"El Administrador {User.Identity?.Name ?? "Admin"} cambió el estado de la cuenta {usuario.Username} a {statusText}");

        return Ok(new
        {
            message = $"La cuenta de {usuario.FullName} ha sido {statusText}.",
            activo = usuario.Activo
        });
    }

    /// <summary>
    /// Consulta todas las materias/cursos registrados en la institución (CRUD Administrador).
    /// GET: /api/admin/courses
    /// </summary>
    [HttpGet("courses")]
    [ProducesResponseType(typeof(List<AdminCourseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminCourses()
    {
        var courses = await _context.Cursos
            .Include(c => c.Grado)
            .Include(c => c.Docente)
            .Include(c => c.Inscripciones.Where(i => i.Estado == "ACTIVA"))
            .OrderBy(c => c.Grado.Nombre)
            .ThenBy(c => c.Nombre)
            .Select(c => new AdminCourseDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Grado = c.Grado.Nombre,
                Grupo = c.Grupo,
                DocenteId = c.DocenteId,
                DocenteNombre = !string.IsNullOrWhiteSpace(c.Docente.FullName) ? c.Docente.FullName : c.Docente.Username,
                Descripcion = c.Descripcion,
                TotalStudents = c.Inscripciones.Count,
                FechaCreacion = c.FechaCreacion,
                Activo = c.Activo
            })
            .ToListAsync();

        return Ok(courses);
    }

    /// <summary>
    /// Crea una nueva materia/curso institucional y le asigna un docente.
    /// POST: /api/admin/courses
    /// </summary>
    [HttpPost("courses")]
    [ProducesResponseType(typeof(AdminCourseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAdminCourse([FromBody] CreateAdminCourseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { message = "El nombre de la materia es requerido." });

        var rawGrade = dto.Grado?.Trim() ?? "1°";
        var gradeKey = rawGrade.Contains('°') ? rawGrade.Substring(0, rawGrade.IndexOf('°') + 1) : rawGrade;
        var grados = await _context.Grados.ToListAsync();
        var grado = grados.FirstOrDefault(g => g.Nombre == gradeKey || g.Nombre == rawGrade);

        if (grado == null)
        {
            return BadRequest(new
            {
                code = "INVALID_GRADE",
                message = $"El grado '{rawGrade}' no existe. Selecciona uno de los grados registrados.",
                validGrades = grados.Select(g => g.Nombre).OrderBy(g => g).ToArray()
            });
        }

        Usuario? docente = null;
        if (dto.DocenteId.HasValue && dto.DocenteId.Value > 0)
        {
            docente = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == dto.DocenteId.Value && u.Rol.Nombre == "DOCENTE");
        }

        if (docente == null)
        {
            docente = await _context.Usuarios.FirstOrDefaultAsync(u => u.Rol.Nombre == "DOCENTE" && u.Activo)
                      ?? await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == User.Identity!.Name);
        }

        if (docente == null)
            return BadRequest(new { message = "No se encontró un docente para asignar a este curso." });

        var curso = new Curso
        {
            Nombre = dto.Nombre.Trim(),
            GradoId = grado.Id,
            Grupo = string.IsNullOrWhiteSpace(dto.Grupo) ? "A" : dto.Grupo.Trim().ToUpperInvariant(),
            DocenteId = docente.Id,
            Descripcion = dto.Descripcion?.Trim(),
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Cursos.Add(curso);
        await _context.SaveChangesAsync();
        InvalidateAdminCache();

        var result = new AdminCourseDto
        {
            Id = curso.Id,
            Nombre = curso.Nombre,
            Grado = grado.Nombre,
            Grupo = curso.Grupo,
            DocenteId = docente.Id,
            DocenteNombre = !string.IsNullOrWhiteSpace(docente.FullName) ? docente.FullName : docente.Username,
            Descripcion = curso.Descripcion,
            TotalStudents = 0,
            FechaCreacion = curso.FechaCreacion,
            Activo = curso.Activo
        };

        _logger.LogInformation("[AUDIT] El Administrador {Admin} creó exitosamente la materia/curso '{NombreCurso}' (ID: {CursoId})",
            User.Identity?.Name ?? "Admin", curso.Nombre, curso.Id);
        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            "Creación de Curso Institucional",
            $"Curso '{curso.Nombre}' ({grado.Nombre}-{curso.Grupo}) asignado a {result.DocenteNombre} (ID: {curso.Id})");
        await FileLogger.LogAsync("AUDIT", $"El Administrador {User.Identity?.Name ?? "Admin"} creó la materia/curso '{curso.Nombre}' (ID: {curso.Id})");

        return CreatedAtAction(nameof(GetAdminCourses), new { id = curso.Id }, result);
    }

    /// <summary>
    /// Actualiza la información de un curso institucional.
    /// PUT: /api/admin/courses/{id}
    /// </summary>
    [HttpPut("courses/{id:int}")]
    public async Task<IActionResult> UpdateAdminCourse(int id, [FromBody] UpdateAdminCourseDto dto)
    {
        var curso = await _context.Cursos.FindAsync(id);
        if (curso == null) return NotFound(new { message = "Curso no encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.Nombre)) curso.Nombre = dto.Nombre.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Grupo)) curso.Grupo = dto.Grupo.Trim().ToUpperInvariant();
        curso.Descripcion = dto.Descripcion?.Trim();

        if (dto.DocenteId.HasValue && dto.DocenteId.Value > 0)
        {
            var docente = await _context.Usuarios.FindAsync(dto.DocenteId.Value);
            if (docente != null) curso.DocenteId = docente.Id;
        }

        if (!string.IsNullOrWhiteSpace(dto.Grado))
        {
            var rawGrade = dto.Grado.Trim();
            var gradeKey = rawGrade.Contains('°') ? rawGrade.Substring(0, rawGrade.IndexOf('°') + 1) : rawGrade;
            var grados = await _context.Grados.ToListAsync();
            var grado = grados.FirstOrDefault(g => g.Nombre == gradeKey || g.Nombre == rawGrade);

            if (grado == null)
            {
                return BadRequest(new
                {
                    code = "INVALID_GRADE",
                    message = $"El grado '{rawGrade}' no existe. Selecciona uno de los grados registrados.",
                    validGrades = grados.Select(g => g.Nombre).OrderBy(g => g).ToArray()
                });
            }

            curso.GradoId = grado.Id;
        }

        await _context.SaveChangesAsync();
        InvalidateAdminCache();
        _logger.LogInformation("[AUDIT] El Administrador {Admin} editó/actualizó la materia/curso '{NombreCurso}' (ID: {CursoId})",
            User.Identity?.Name ?? "Admin", curso.Nombre, id);
        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            "Edición de Curso Institucional",
            $"Curso ID {id} actualizado ('{curso.Nombre}', Grupo {curso.Grupo})");
        await FileLogger.LogAsync("AUDIT", $"El Administrador {User.Identity?.Name ?? "Admin"} actualizó la materia/curso '{curso.Nombre}' (ID: {id})");

        return Ok(new { message = "Curso actualizado correctamente." });
    }

    /// <summary>
    /// Activa o desactiva un curso institucional.
    /// PATCH: /api/admin/courses/{id}/toggle-status
    /// </summary>
    [HttpPatch("courses/{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleCourseStatus(int id)
    {
        var curso = await _context.Cursos.FindAsync(id);
        if (curso == null) return NotFound(new { message = "Curso no encontrado." });

        curso.Activo = !curso.Activo;
        await _context.SaveChangesAsync();
        InvalidateAdminCache();

        var statusText = curso.Activo ? "activado" : "desactivado";
        _logger.LogInformation("[AUDIT] El Administrador {Admin} alternó el estado del curso '{NombreCurso}' (ID: {CursoId}) a {StatusText}",
            User.Identity?.Name ?? "Admin", curso.Nombre, curso.Id, statusText);
        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            $"Estado de Curso ({statusText.ToUpperInvariant()})",
            $"Curso '{curso.Nombre}' (ID {curso.Id}) {statusText} por {User.Identity?.Name ?? "Admin"}");
        await FileLogger.LogAsync("AUDIT", $"El Administrador {User.Identity?.Name ?? "Admin"} alternó el estado del curso '{curso.Nombre}' (ID: {curso.Id}) a {statusText}");

        return Ok(new
        {
            message = $"El curso '{curso.Nombre}' ha sido {statusText}.",
            activo = curso.Activo
        });
    }

    /// <summary>
    /// Estadísticas globales de actividad y rendimiento para dashboards y visualizaciones.
    /// GET: /api/admin/activity-stats
    /// </summary>
    [HttpGet("activity-stats")]
    [ProducesResponseType(typeof(AdminActivityStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivityStats()
    {
        if (_cache.TryGetValue("admin_activity_stats", out AdminActivityStatsDto? cached) && cached != null)
        {
            return Ok(cached);
        }

        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        var tasksCreatedLast30 = await _context.Tareas.AsNoTracking().CountAsync(t => t.FechaPublicacion >= thirtyDaysAgo);
        var submissionsLast30 = await _context.Entregas.AsNoTracking().CountAsync(e => e.FechaEntrega >= thirtyDaysAgo);

        // Entregas por semana (últimas 8 semanas calendario)
        var weeklySubmissions = new List<SubmissionsPerWeekDto>();
        for (int i = 7; i >= 0; i--)
        {
            var weekStart = now.Date.AddDays(-(int)now.DayOfWeek + 1).AddDays(-7 * i);
            var weekEnd = weekStart.AddDays(7);
            var count = await _context.Entregas.AsNoTracking()
                .CountAsync(e => e.FechaEntrega >= weekStart && e.FechaEntrega < weekEnd);

            weeklySubmissions.Add(new SubmissionsPerWeekDto
            {
                WeekStart = weekStart.ToString("yyyy-MM-dd"),
                Count = count
            });
        }

        // Distribución de calificaciones
        var gradedEntregas = await _context.Entregas.AsNoTracking()
            .Where(e => e.Calificacion != null)
            .Select(e => e.Calificacion!.Value)
            .ToListAsync();

        var gradeDistribution = new List<GradeDistributionDto>
        {
            new() { Range = "0.0-2.9", Count = gradedEntregas.Count(g => g < 3.0m) },
            new() { Range = "3.0-3.9", Count = gradedEntregas.Count(g => g >= 3.0m && g < 4.0m) },
            new() { Range = "4.0-4.5", Count = gradedEntregas.Count(g => g >= 4.0m && g < 4.6m) },
            new() { Range = "4.6-5.0", Count = gradedEntregas.Count(g => g >= 4.6m) }
        };

        // Estudiantes activos agrupados por grado
        var studentsByGrade = await _context.Alumnos.AsNoTracking()
            .Where(a => a.Usuario.Activo && a.Usuario.AnonymizedAt == null)
            .GroupBy(a => a.Grado != null ? a.Grado.Nombre : "Sin Grado")
            .Select(g => new ActiveStudentsByGradeDto
            {
                Grade = g.Key,
                Count = g.Count()
            })
            .OrderBy(g => g.Grade)
            .ToListAsync();

        var stats = new AdminActivityStatsDto
        {
            SubmissionsPerWeek = weeklySubmissions,
            GradeDistribution = gradeDistribution,
            ActiveStudentsByGrade = studentsByGrade,
            TasksCreatedLast30Days = tasksCreatedLast30,
            SubmissionsLast30Days = submissionsLast30
        };

        _cache.Set("admin_activity_stats", stats, TimeSpan.FromSeconds(60));
        return Ok(stats);
    }

    [HttpGet("backup-database")]
    [Produces("application/sql")]
    public async Task<IActionResult> BackupDatabase()
    {
        try
        {
            var sqlBuilder = new StringBuilder();
            var timestamp = DateTime.UtcNow;

            sqlBuilder.AppendLine("-- ====================================================================");
            sqlBuilder.AppendLine("-- LMS PRIMARIA - RESPALDO / BACKUP DE BASE DE DATOS");
            sqlBuilder.AppendLine($"-- FECHA DE EXTRACCIÓN (UTC): {timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sqlBuilder.AppendLine("-- COPIA DE SEGURIDAD INSTITUCIONAL SCIKIDS LMS");
            sqlBuilder.AppendLine("-- ADVERTENCIA: este archivo contiene DATOS PERSONALES de menores de edad");
            sqlBuilder.AppendLine("-- y hashes de contraseñas. Almacénelo cifrado, con acceso restringido y");
            sqlBuilder.AppendLine("-- elimínelo según la política de retención (Ley 1581 de 2012, art. 17).");
            sqlBuilder.AppendLine("-- ====================================================================");
            sqlBuilder.AppendLine();
            sqlBuilder.AppendLine("SET statement_timeout = 0;");
            sqlBuilder.AppendLine("SET client_encoding = 'UTF8';");
            sqlBuilder.AppendLine("BEGIN;");
            sqlBuilder.AppendLine();

            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            var canonicalOrder = new List<string>
            {
                "roles", "grados", "usuarios", "alumnos", "cursos", "inscripciones", "tareas", "entregas", "archivos_entrega", "materiales"
            };

            var existingTables = new List<string>();
            using (var tablesCmd = conn.CreateCommand())
            {
                tablesCmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name;";
                using var reader = await tablesCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) existingTables.Add(reader.GetString(0));
            }

            var orderedTables = canonicalOrder.Where(t => existingTables.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
            foreach (var tbl in existingTables)
            {
                if (!orderedTables.Contains(tbl, StringComparer.OrdinalIgnoreCase)) orderedTables.Add(tbl);
            }

            foreach (var tableName in orderedTables)
            {
                sqlBuilder.AppendLine($"-- TABLA: \"{tableName}\"");
                var columns = new List<string>();
                using (var colCmd = conn.CreateCommand())
                {
                    colCmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @tbl ORDER BY ordinal_position;";
                    var p = colCmd.CreateParameter();
                    p.ParameterName = "@tbl";
                    p.Value = tableName;
                    colCmd.Parameters.Add(p);

                    using var colReader = await colCmd.ExecuteReaderAsync();
                    while (await colReader.ReadAsync()) columns.Add(colReader.GetString(0));
                }

                if (columns.Count == 0) continue;

                using (var dataCmd = conn.CreateCommand())
                {
                    dataCmd.CommandText = $"SELECT * FROM \"{tableName}\";";
                    using var dataReader = await dataCmd.ExecuteReaderAsync();
                    var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
                    var rowsDumped = 0;

                    while (await dataReader.ReadAsync())
                    {
                        var values = new List<string>();
                        for (int i = 0; i < dataReader.FieldCount; i++)
                        {
                            if (dataReader.IsDBNull(i)) values.Add("NULL");
                            else
                            {
                                var val = dataReader.GetValue(i);
                                switch (val)
                                {
                                    case bool b: values.Add(b ? "TRUE" : "FALSE"); break;
                                    case int or long or short or byte: values.Add(val.ToString()!); break;
                                    case decimal or double or float: values.Add(Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture)!); break;
                                    case DateTime dt: values.Add($"'{dt:yyyy-MM-dd HH:mm:ss.ffffff}'"); break;
                                    case DateTimeOffset dto: values.Add($"'{dto:yyyy-MM-dd HH:mm:ss.ffffffzzz}'"); break;
                                    case Guid g: values.Add($"'{g}'"); break;
                                    default: values.Add($"'{val.ToString()?.Replace("'", "''")}'"); break;
                                }
                            }
                        }
                        sqlBuilder.AppendLine($"INSERT INTO \"{tableName}\" ({columnList}) VALUES ({string.Join(", ", values)}) ON CONFLICT DO NOTHING;");
                        rowsDumped++;
                    }
                    sqlBuilder.AppendLine($"-- Registros exportados: {rowsDumped}");
                    sqlBuilder.AppendLine();
                }
            }

            sqlBuilder.AppendLine("COMMIT;");
            var sqlBytes = Encoding.UTF8.GetBytes(sqlBuilder.ToString());
            var fileName = $"respaldo_scikids_{timestamp:yyyyMMdd_HHmmss}.sql";

            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, private";

            await AuditAsync("Administración: Descarga de respaldo de base de datos",
                $"Archivo {fileName} ({sqlBytes.Length} bytes). Contiene datos personales: acceso restringido.");

            return File(sqlBytes, "application/sql", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando copia de seguridad SQL.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Error interno al generar la copia de seguridad SQL.",
                detail = _env.IsDevelopment() ? ex.Message : null
            });
        }
    }

    /// <summary>
    /// Restauración de respaldo desde la interfaz.
    /// Deshabilitada por defecto: ejecutar SQL arbitrario desde una carga HTTP es una
    /// vulnerabilidad de ejecución remota sobre la base de datos (OWASP A03:2021).
    /// El procedimiento recomendado es scripts/restaurar_backup.ps1 con psql y un rol
    /// de base de datos de mínimo privilegio. Solo se habilita explícitamente en desarrollo.
    /// </summary>
    [HttpPost("restore-backup")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RestoreBackup(IFormFile file, [FromForm] string? confirmation)
    {
        if (!_configuration.GetValue<bool>("Features:EnableDatabaseRestore"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                allowed = false,
                message = "La restauración de la base de datos desde la interfaz está deshabilitada por seguridad. " +
                          "Use el procedimiento documentado en docs/OPERACION_BACKUP.md (psql con un rol de mínimo privilegio)."
            });
        }

        if (!_env.IsDevelopment() && !_configuration.GetValue<bool>("Features:AllowRestoreInProduction"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                allowed = false,
                message = "La restauración solo está permitida en desarrollo o con Features:AllowRestoreInProduction habilitado."
            });
        }

        if (!string.Equals(confirmation?.Trim(), "RESTAURAR", StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Debes escribir la palabra RESTAURAR para confirmar la operación." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Debes seleccionar un archivo de copia de seguridad (.sql) válido." });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { message = "El archivo de restauración no puede superar 5 MB." });
        }

        if (!file.FileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Formato no permitido. El archivo debe tener extensión .sql" });
        }

        try
        {
            string sqlContent;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                sqlContent = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(sqlContent))
            {
                return BadRequest(new { message = "El archivo SQL proporcionado está vacío." });
            }

            // Bloqueo de sentencias propias de escalamiento o acceso al sistema operativo.
            var blockedPatterns = new[]
            {
                @"\bCOPY\b[\s\S]{0,200}\bPROGRAM\b",
                @"\bALTER\s+SYSTEM\b",
                @"\bCREATE\s+(ROLE|USER)\b",
                @"\bDROP\s+(DATABASE|ROLE)\b",
                @"\bpg_read_file\b",
                @"\bpg_ls_dir\b",
                @"\blo_import\b",
                @"\blo_export\b",
                @"\bdblink\b"
            };

            foreach (var pattern in blockedPatterns)
            {
                if (Regex.IsMatch(sqlContent, pattern, RegexOptions.IgnoreCase))
                {
                    _logger.LogWarning("Restauración rechazada: el script contiene una sentencia bloqueada ({Pattern}).", pattern);
                    await AuditAsync("Administración: Restauración rechazada", $"Sentencia bloqueada detectada: {pattern}", "WARNING");

                    return BadRequest(new
                    {
                        message = "El script contiene sentencias no permitidas por la política de seguridad."
                    });
                }
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;
            cmd.CommandText = sqlContent;

            await cmd.ExecuteNonQueryAsync();

            await AuditAsync("Administración: Restauración de base de datos",
                $"Archivo {file.FileName} ejecutado en entorno {_env.EnvironmentName}.", "WARNING");

            return Ok(new
            {
                message = "La base de datos se ha restaurado exitosamente desde el archivo SQL subido.",
                fileName = file.FileName,
                restoredAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar la restauración del archivo SQL.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Ocurrió un error al restaurar la copia de seguridad."
            });
        }
    }

    /// <summary>
    /// Resumen de cumplimiento para el responsable del tratamiento y auditorías.
    /// GET: /api/admin/compliance
    /// </summary>
    [HttpGet("compliance")]
    public async Task<IActionResult> GetComplianceSummary()
    {
        var policyVersion = _configuration["Privacy:PolicyVersion"] ?? "sin-configurar";

        var totalUsers = await _context.Usuarios.CountAsync();
        var activeUsers = await _context.Usuarios.CountAsync(u => u.Activo);
        var consentedUsers = await _context.Usuarios.CountAsync(u => u.DataPolicyAccepted);
        var currentConsentUsers = await _context.Usuarios
            .CountAsync(u => u.DataPolicyAccepted && u.DataPolicyVersion == policyVersion);
        var anonymizedUsers = await _context.Usuarios.CountAsync(u => u.AnonymizedAt != null);

        var defaultAccounts = await _context.Usuarios
            .Where(u => u.Username == "admin" || u.Username == "docente" || u.Username == "estudiante")
            .Select(u => u.Username)
            .ToListAsync();

        var pendingConsent = totalUsers - currentConsentUsers;

        return Ok(new
        {
            policyVersion,
            generatedAt = DateTime.UtcNow,
            users = new
            {
                total = totalUsers,
                active = activeUsers,
                anonymized = anonymizedUsers
            },
            consent = new
            {
                acceptedEver = consentedUsers,
                acceptedCurrentVersion = currentConsentUsers,
                pending = pendingConsent,
                coveragePercentage = totalUsers == 0 ? 100m : Math.Round((decimal)currentConsentUsers / totalUsers * 100m, 1)
            },
            warnings = new[]
            {
                defaultAccounts.Count > 0
                    ? $"Existen cuentas con identificadores por defecto: {string.Join(", ", defaultAccounts)}. Rote sus contraseñas."
                    : null,
                pendingConsent > 0
                    ? $"{pendingConsent} usuario(s) no han aceptado la versión vigente de la política."
                    : null
            }.Where(w => w != null),
            audit = new
            {
                retentionDays = _configuration.GetValue<int?>("Privacy:LogRetentionDays") ?? 90,
                location = FileLogger.GetAuditLogPath()
            }
        });
    }

    /// <summary>
    /// Consulta y filtra los logs estructurados de auditoría del sistema en tiempo real.
    /// GET: /api/admin/audit-logs
    /// </summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(AuditLogResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? level = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? role = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 5, 100);

        try
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(level) && !string.Equals(level, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var lvlUpper = level.ToUpperInvariant();
                query = query.Where(a => a.Level.ToUpper() == lvlUpper);
            }

            if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var roleUpper = role.ToUpperInvariant();
                query = query.Where(a => a.UserRole != null && a.UserRole.ToUpper() == roleUpper);
            }

            if (from.HasValue)
            {
                var fromUtc = from.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc)
                    : from.Value.ToUniversalTime();
                query = query.Where(a => a.Timestamp >= fromUtc);
            }

            if (to.HasValue)
            {
                var toUtc = to.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc)
                    : to.Value.ToUniversalTime();
                query = query.Where(a => a.Timestamp <= toUtc);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLowerInvariant();
                query = query.Where(a => a.Action.ToLower().Contains(kw) ||
                                         a.Path.ToLower().Contains(kw) ||
                                         (a.UserEmail != null && a.UserEmail.ToLower().Contains(kw)) ||
                                         (a.UserId != null && a.UserId.ToLower().Contains(kw)) ||
                                         a.Ip.Contains(kw) ||
                                         (a.Details != null && a.Details.ToLower().Contains(kw)));
            }

            var totalCount = await query.CountAsync();
            if (totalCount > 0)
            {
                var items = await query
                    .OrderByDescending(a => a.Timestamp)
                    .ThenByDescending(a => a.Id)
                    .Skip((safePage - 1) * safePageSize)
                    .Take(safePageSize)
                    .Select(a => new AuditLogEntry
                    {
                        Id = a.Id.ToString(),
                        Timestamp = a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        Level = a.Level,
                        Method = a.Method,
                        Path = a.Path,
                        StatusCode = a.StatusCode,
                        ElapsedMs = a.ElapsedMs,
                        Ip = a.Ip,
                        UserId = a.UserId,
                        UserEmail = a.UserEmail,
                        UserRole = a.UserRole,
                        Action = a.Action,
                        Details = a.Details
                    })
                    .ToListAsync();

                return Ok(new AuditLogResult
                {
                    Page = safePage,
                    PageSize = safePageSize,
                    TotalCount = totalCount,
                    Items = items
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al consultar auditoría en base de datos. Usando archivo como respaldo.");
        }

        var result = await FileLogger.ReadAuditLogsAsync(
            page: safePage,
            pageSize: safePageSize,
            level: level,
            keyword: keyword,
            role: role,
            fromDate: from,
            toDate: to
        );

        return Ok(result);
    }

    /// <summary>
    /// Descarga el archivo físico consolidado de auditoría del sistema (/logs/app-audit.log).
    /// GET: /api/admin/audit-logs/download
    /// </summary>
    [HttpGet("audit-logs/download")]
    public IActionResult DownloadAuditLogs()
    {
        var auditPath = FileLogger.GetAuditLogPath();
        if (!System.IO.File.Exists(auditPath))
        {
            var accessPath = Path.Combine(FileLogger.LogDirectory, "access.log");
            if (System.IO.File.Exists(accessPath))
            {
                var accessStream = new FileStream(accessPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return File(accessStream, "text/plain", $"access-log-{DateTime.UtcNow:yyyyMMdd-HHmm}.log");
            }

            return NotFound(new { message = "No se encontraron archivos de registro de auditoría en el servidor." });
        }

        var stream = new FileStream(auditPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(stream, "text/plain", $"app-audit-{DateTime.UtcNow:yyyyMMdd-HHmm}.log");
    }

    private static bool IsAdminRole(string? roleName)
        => roleName is not null
           && (roleName.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase)
               || roleName.Equals(".admin", StringComparison.OrdinalIgnoreCase)
               || roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Predicado equivalente a <see cref="IsAdminRole"/> que EF Core traduce a SQL.
    /// Incluye el rol legado "Admin" y compara sin distinguir mayúsculas.
    /// </summary>
    private static readonly System.Linq.Expressions.Expression<Func<Usuario, bool>> AdminRolePredicate =
        u => u.Rol.Nombre.ToLower() == "administrador"
             || u.Rol.Nombre.ToLower() == ".admin"
             || u.Rol.Nombre.ToLower() == "admin";

    private async Task AuditAsync(string action, string? details = null, string level = "AUDIT")
    {
        try
        {
            RequestLoggingMiddleware.SetAuditContext(
                HttpContext,
                action,
                details,
                User.Identity?.Name,
                User.Identity?.Name?.Contains('@') == true ? User.Identity.Name : null,
                User.FindFirstValue(ClaimTypes.Role));

            await FileLogger.LogAsync(level, $"[Admin] {action} | Actor: {User.Identity?.Name ?? "Admin"} | {details}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible registrar la auditoría administrativa.");
        }
    }
}