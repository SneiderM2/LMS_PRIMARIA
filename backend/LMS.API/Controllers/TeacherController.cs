using System.Security.Claims;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Middleware;
using LMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Teacher,ADMINISTRADOR,DOCENTE")]
public class TeacherController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly ISemaforoService _semaforoService;
    private readonly IFileStorageService _fileStorage;
    private readonly ConsentService _consent;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TeacherController> _logger;

    public TeacherController(
        LMSDbContext context,
        ISemaforoService semaforoService,
        IFileStorageService fileStorage,
        ConsentService consent,
        IMemoryCache cache,
        ILogger<TeacherController> logger)
    {
        _context = context;
        _semaforoService = semaforoService;
        _fileStorage = fileStorage;
        _consent = consent;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Usuario autenticado con su rol (para validar pertenencia de cursos y evitar IDOR).
    /// </summary>
    private async Task<Usuario?> GetCurrentUserAsync()
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

    /// <summary>
    /// Un docente solo puede gestionar cursos que tiene asignados; un administrador, cualquiera.
    /// </summary>
    private async Task<bool> CanManageCourseAsync(int courseId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return false;
        }

        if (IsAdmin(user.Rol?.Nombre))
        {
            return true;
        }

        return await _context.Cursos.AnyAsync(c => c.Id == courseId && c.DocenteId == user.Id);
    }

    /// <summary>
    /// Lista todos los estudiantes registrados en la plataforma disponibles para matricular
    /// </summary>
    [HttpGet("students")]
    [ProducesResponseType(typeof(List<TeacherStudentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllStudents([FromQuery] string? grade = null)
    {
        var query = _context.Alumnos
            .Include(a => a.Usuario)
            .Include(a => a.Grado)
            .Include(a => a.Inscripciones)
                .ThenInclude(i => i.Curso)
            .Where(a => a.Usuario.Activo)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(grade))
        {
            query = query.Where(a => a.Grado.Nombre == grade || a.Grado.Nombre.Contains(grade));
        }

        var students = await query
            .OrderBy(a => a.Grado.Nombre)
            .ThenBy(a => !string.IsNullOrEmpty(a.Usuario.Nombre) ? a.Usuario.Nombre : a.Usuario.Username)
            .Select(a => new TeacherStudentDto
            {
                Id = a.UsuarioId,
                Username = a.Usuario.Username,
                FullName = !string.IsNullOrWhiteSpace(a.Usuario.FullName) 
                    ? a.Usuario.FullName 
                    : a.Usuario.Username,
                Grade = a.Grado.Nombre,
                AvatarUrl = !string.IsNullOrEmpty(a.Usuario.AvatarUrl)
                    ? a.Usuario.AvatarUrl
                    : $"https://api.dicebear.com/7.x/bottts/svg?seed={a.Usuario.Username}",
                CreatedAt = a.Usuario.FechaCreacion,
                EnrolledCourses = a.Inscripciones
                    .Where(i => i.Estado == "ACTIVA")
                    .Select(i => new EnrolledCourseSummaryDto
                    {
                        CourseId = i.CursoId,
                        CourseName = i.Curso.Nombre,
                        Group = i.Curso.Grupo,
                        Status = i.Estado
                    }).ToList()
            })
            .ToListAsync();

        var accessor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Docente";
        await FileLogger.LogAsync("AUDIT",
            $"[Ley 1581 / Ley 1098] Acceso a listado sensible de estudiantes menores ({students.Count} registros, filtro grado='{grade ?? "TODOS"}') por {accessor}.");

        return Ok(students);
    }

    /// <summary>
    /// Registra o crea un nuevo estudiante directamente en la plataforma desde el portal docente
    /// </summary>
    [HttpPost("students")]
    [ProducesResponseType(typeof(TeacherStudentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cleanUsername = dto.Username.Trim();
        var exists = await _context.Usuarios.AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower());
        if (exists)
        {
            return BadRequest(new { message = "El documento o carnet escolar ya existe en el sistema." });
        }

        var alumnoRol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == "ALUMNO");
        if (alumnoRol == null)
        {
            alumnoRol = new Rol { Nombre = "ALUMNO", Descripcion = "Consulta cursos y entrega actividades" };
            _context.Roles.Add(alumnoRol);
            await _context.SaveChangesAsync();
        }

        var rawGrade = dto.Grade.Trim();
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

        var password = dto.Password?.Trim();
        var generatedPassword = false;

        if (string.IsNullOrWhiteSpace(password))
        {
            password = PasswordPolicy.GenerateTemporaryPassword();
            generatedPassword = true;
        }
        else
        {
            var passwordCheck = PasswordPolicy.Validate(password, cleanUsername, dto.FullName);
            if (!passwordCheck.IsValid)
            {
                return BadRequest(new { code = "WEAK_PASSWORD", message = passwordCheck.Message });
            }
        }

        var parts = dto.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = parts.Length > 0 ? parts[0] : cleanUsername;
        var lastName = parts.Length > 1 ? parts[1] : "";
        var avatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(cleanUsername)}";

        var hasGuardianConsent = dto.GuardianAuthorization && !string.IsNullOrWhiteSpace(dto.GuardianFullName);
        if (hasGuardianConsent)
        {
            var guardianCheck = ConsentService.ValidateGuardianConsent(
                dto.GuardianAuthorization,
                dto.GuardianFullName,
                dto.GuardianDocument);

            if (!guardianCheck.IsValid)
            {
                return BadRequest(new { code = "GUARDIAN_CONSENT_REQUIRED", message = guardianCheck.Message });
            }
        }

        var nuevoUsuario = new Usuario
        {
            Username = cleanUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            RolId = alumnoRol.Id,
            Nombre = firstName,
            Apellido = lastName,
            AvatarUrl = avatarUrl,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            DataPolicyAccepted = hasGuardianConsent,
            DataPolicyAcceptedAt = hasGuardianConsent ? DateTime.UtcNow : null,
            DataPolicyVersion = hasGuardianConsent ? _consent.CurrentVersion : null
        };
        _context.Usuarios.Add(nuevoUsuario);
        await _context.SaveChangesAsync();

        if (hasGuardianConsent)
        {
            nuevoUsuario.DataPolicyEvidence = _consent.BuildEvidence(
                nuevoUsuario,
                "teacher-portal-registration",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                dto.GuardianFullName,
                dto.GuardianDocument);
        }

        var alumno = new Alumno
        {
            UsuarioId = nuevoUsuario.Id,
            GradoId = grado.Id
        };
        _context.Alumnos.Add(alumno);

        if (hasGuardianConsent)
        {
            var consentRecord = _consent.CreateHabeasDataRecord(
                alumnoId: nuevoUsuario.Id,
                usuarioId: nuevoUsuario.Id,
                guardianFullName: dto.GuardianFullName,
                guardianDocument: dto.GuardianDocument,
                tipoDocumento: "CC",
                autorizaServicioEducativo: true,
                autorizaDatosOpcionales: false,
                autorizaTransmisionInternacional: true,
                ip: HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                userAgent: HttpContext?.Request?.Headers.UserAgent.ToString());

            _context.ConsentimientosHabeasData.Add(consentRecord);
        }

        await _context.SaveChangesAsync();

        var creator = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Docente";
        await FileLogger.LogAsync("AUDIT",
            $"[Ley 1581 / Ley 1098] Registro de estudiante menor {nuevoUsuario.Username} ({nuevoUsuario.FullName}, grado {grado.Nombre}) por {creator}. Consentimiento acudiente en registro: {hasGuardianConsent}.");

        var resultDto = new TeacherStudentDto
        {
            Id = nuevoUsuario.Id,
            Username = nuevoUsuario.Username,
            FullName = nuevoUsuario.FullName,
            Grade = grado.Nombre,
            AvatarUrl = avatarUrl,
            CreatedAt = nuevoUsuario.FechaCreacion,
            EnrolledCourses = new List<EnrolledCourseSummaryDto>(),
            TemporaryPassword = generatedPassword ? password : null
        };

        return CreatedAtAction(nameof(GetAllStudents), new { id = nuevoUsuario.Id }, resultDto);
    }

    /// <summary>
    /// Asigna o matricula a un estudiante a una materia/curso específico
    /// </summary>
    [HttpPost("courses/{courseId:int}/enroll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnrollStudent(int courseId, [FromBody] EnrollStudentDto dto)
    {
        var curso = await _context.Cursos.FindAsync(courseId);
        if (curso == null)
        {
            return NotFound(new { message = "El curso o materia especificado no existe." });
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Unauthorized();

        if (!IsAdmin(currentUser.Rol?.Nombre) && curso.DocenteId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Solo puedes matricular estudiantes en los cursos que tienes asignados."
            });
        }

        // Buscar alumno por ID numérico o por username (carnet)
        Alumno? alumno = null;
        if (int.TryParse(dto.StudentId, out var parsedId))
        {
            alumno = await _context.Alumnos
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.UsuarioId == parsedId);
        }

        if (alumno == null)
        {
            var cleanId = dto.StudentId.Trim().ToLower();
            alumno = await _context.Alumnos
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.Usuario.Username.ToLower() == cleanId);
        }

        if (alumno == null)
        {
            return NotFound(new { message = "Estudiante no encontrado en el sistema." });
        }

        // Comprobar si ya está inscrito
        var inscripcion = await _context.Inscripciones
            .FirstOrDefaultAsync(i => i.CursoId == courseId && i.AlumnoId == alumno.UsuarioId);

        if (inscripcion != null)
        {
            if (inscripcion.Estado == "ACTIVA")
            {
                return Ok(new { message = "El estudiante ya se encuentra matriculado en este curso.", yaMatriculado = true });
            }
            else
            {
                inscripcion.Estado = "ACTIVA";
                inscripcion.FechaRegistro = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("[AUDIT] El Docente {UserId} reactivó la matrícula del Estudiante {AlumnoId} en el curso {CursoId}",
                    currentUser.Username, alumno.UsuarioId, curso.Id);
                await FileLogger.LogAsync("AUDIT", $"El Docente {currentUser.Username} reactivó la matrícula del Estudiante {alumno.UsuarioId} en el curso {curso.Id}");

                return Ok(new { message = "Matrícula reactivada con éxito para el estudiante.", yaMatriculado = false });
            }
        }

        // Crear nueva inscripción
        var nuevaInscripcion = new Inscripcion
        {
            CursoId = courseId,
            AlumnoId = alumno.UsuarioId,
            FechaRegistro = DateTime.UtcNow,
            Estado = "ACTIVA"
        };
        _context.Inscripciones.Add(nuevaInscripcion);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] El Docente {UserId} asignó/matriculó al Estudiante {AlumnoId} en el curso {CursoId}",
            currentUser.Username, alumno.UsuarioId, curso.Id);
        await FileLogger.LogAsync("AUDIT", $"El Docente {currentUser.Username} asignó/matriculó al Estudiante {alumno.UsuarioId} en el curso {curso.Id}");

        var studentName = !string.IsNullOrWhiteSpace(alumno.Usuario.FullName) 
            ? alumno.Usuario.FullName 
            : alumno.Usuario.Username;

        return Ok(new
        {
            message = $"¡{studentName} ha sido matriculado(a) exitosamente en {curso.Nombre}!",
            courseId = curso.Id,
            courseName = curso.Nombre,
            studentId = alumno.UsuarioId,
            studentName = studentName
        });
    }

    /// <summary>
    /// Retorna los cursos dictados por el docente actual o todos si es Administrador
    /// </summary>
    [HttpGet("courses")]
    [ProducesResponseType(typeof(List<CourseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourses()
    {
        var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUser = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Username == currentUsername);

        var query = _context.Cursos
            .Include(c => c.Grado)
            .Include(c => c.Docente)
            .Include(c => c.Inscripciones.Where(i => i.Estado == "ACTIVA"))
            .Where(c => c.Activo)
            .AsQueryable();

        // Si no es Administrador, filtrar estrictamente solo sus cursos asignados
        if (currentUser != null && !IsAdmin(currentUser.Rol?.Nombre))
        {
            query = query.Where(c => c.DocenteId == currentUser.Id);
        }

        var courses = await query
            .OrderBy(c => c.Grado.Nombre)
            .ThenBy(c => c.Nombre)
            .Select(c => new CourseDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Grado = c.Grado.Nombre,
                Grupo = c.Grupo,
                DocenteId = c.DocenteId,
                DocenteNombre = !string.IsNullOrWhiteSpace(c.Docente.FullName) 
                    ? c.Docente.FullName 
                    : c.Docente.Username,
                Descripcion = c.Descripcion,
                TotalStudents = c.Inscripciones.Count,
                FechaCreacion = c.FechaCreacion,
                Activo = c.Activo
            })
            .ToListAsync();

        return Ok(courses);
    }

    /// <summary>
    /// Permite al docente o administrador crear una nueva materia/curso
    /// </summary>
    [HttpPost("courses")]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUser = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Username == currentUsername);

        if (currentUser == null)
        {
            return Unauthorized();
        }

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

        var curso = new Curso
        {
            Nombre = dto.Nombre.Trim(),
            GradoId = grado.Id,
            Grupo = dto.Grupo.Trim().ToUpperInvariant(),
            DocenteId = currentUser.Id,
            Descripcion = dto.Descripcion?.Trim(),
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Cursos.Add(curso);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] El Docente {UserId} creó exitosamente la materia/curso '{NombreCurso}' (ID: {CursoId})",
            currentUser.Username, curso.Nombre, curso.Id);
        await FileLogger.LogAsync("AUDIT", $"El Docente {currentUser.Username} creó exitosamente la materia/curso '{curso.Nombre}' (ID: {curso.Id})");

        var resultDto = new CourseDto
        {
            Id = curso.Id,
            Nombre = curso.Nombre,
            Grado = grado.Nombre,
            Grupo = curso.Grupo,
            DocenteId = currentUser.Id,
            DocenteNombre = !string.IsNullOrWhiteSpace(currentUser.FullName) ? currentUser.FullName : currentUser.Username,
            Descripcion = curso.Descripcion,
            TotalStudents = 0,
            FechaCreacion = curso.FechaCreacion,
            Activo = curso.Activo
        };

        return CreatedAtAction(nameof(GetCourses), new { id = curso.Id }, resultDto);
    }

    /// <summary>
    /// Retorna la lista de estudiantes con su cálculo de estado de semáforo (Verde, Amarillo, Rojo).
    /// Aplica control anti-IDOR: un docente solo consulta el SemaforoStatus de estudiantes de sus cursos/grados.
    /// </summary>
    [HttpGet("students-status")]
    [ProducesResponseType(typeof(List<StudentStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentsStatus([FromQuery] string? gradeLevel = null, [FromQuery] int? courseId = null)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Unauthorized();

        var isAdmin = IsAdmin(currentUser.Rol?.Nombre);
        if (courseId.HasValue && !await CanManageCourseAsync(courseId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "No tienes autorización para consultar el semáforo de un curso asignado a otro docente."
            });
        }

        var cacheKey = $"semaforo_status_{currentUser.Id}_{courseId?.ToString() ?? "ALL"}_{gradeLevel ?? "ALL"}";
        if (_cache.TryGetValue(cacheKey, out List<StudentStatusDto>? cached) && cached != null)
        {
            return Ok(cached);
        }

        var query = _context.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Entregas)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Inscripciones)
            .Where(u => u.Rol.Nombre == "ALUMNO" && u.Activo)
            .AsQueryable();

        if (!isAdmin)
        {
            var teacherCourses = await _context.Cursos
                .AsNoTracking()
                .Where(c => c.DocenteId == currentUser.Id && c.Activo)
                .Select(c => new { c.Id, c.GradoId })
                .ToListAsync();

            var allowedCourseIds = teacherCourses.Select(c => c.Id).ToList();
            var allowedGradeIds = teacherCourses.Select(c => c.GradoId).Distinct().ToList();

            query = query.Where(u => u.Alumno != null && (
                allowedGradeIds.Contains(u.Alumno.GradoId) ||
                u.Alumno.Inscripciones.Any(i => i.Estado == "ACTIVA" && allowedCourseIds.Contains(i.CursoId))));
        }

        if (courseId.HasValue)
        {
            var targetCourseId = courseId.Value;
            query = query.Where(u => u.Alumno != null &&
                u.Alumno.Inscripciones.Any(i => i.CursoId == targetCourseId && i.Estado == "ACTIVA"));
        }

        if (!string.IsNullOrWhiteSpace(gradeLevel))
        {
            var clean = gradeLevel.Contains('°') ? gradeLevel.Substring(0, gradeLevel.IndexOf('°') + 1) : gradeLevel;
            query = query.Where(u => u.Alumno != null && (u.Alumno.Grado.Nombre == clean || u.Alumno.Grado.Nombre == gradeLevel));
        }

        var students = await query
            .OrderBy(u => u.Alumno != null ? u.Alumno.Grado.Nombre : "")
            .ThenBy(u => !string.IsNullOrEmpty(u.Nombre) ? u.Nombre : u.Username)
            .ToListAsync();

        // Calcular última actividad de cada estudiante a partir de sus entregas o registro
        var activityMap = new Dictionary<int, DateTime?>();
        foreach (var s in students)
        {
            var maxEntrega = s.Alumno?.Entregas.OrderByDescending(e => e.FechaEntrega).FirstOrDefault()?.FechaEntrega;
            var maxInscripcion = s.Alumno?.Inscripciones.OrderByDescending(i => i.FechaRegistro).FirstOrDefault()?.FechaRegistro;
            DateTime? lastAct = maxEntrega ?? maxInscripcion ?? s.FechaCreacion;
            activityMap[s.Id] = lastAct;
        }

        var statusList = _semaforoService.CalculateStatusList(students, activityMap);
        _cache.Set(cacheKey, statusList, TimeSpan.FromSeconds(30));

        var accessor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Docente";
        await FileLogger.LogAsync("AUDIT",
            $"[Ley 1581 / Ley 1098] Consulta de semáforo escolar y seguimiento de actividad de menores ({statusList.Count} estudiantes, grado='{gradeLevel ?? "TODOS"}') por {accessor}.");
        return Ok(statusList);
    }

    /// <summary>
    /// Consulta el estado de semáforo de un estudiante específico validando permisos sobre SemaforoStatusId / AlumnoId.
    /// GET: /api/teacher/students-status/{semaforoStatusId:int}
    /// </summary>
    [HttpGet("students-status/{semaforoStatusId:int}")]
    [ProducesResponseType(typeof(StudentStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentSemaforoStatusById(int semaforoStatusId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Unauthorized();

        var studentUser = await _context.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Entregas)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Inscripciones)
            .FirstOrDefaultAsync(u => u.Id == semaforoStatusId && u.Alumno != null);

        if (studentUser == null || studentUser.Alumno == null)
        {
            return NotFound(new { message = "Registro de semáforo del estudiante no encontrado." });
        }

        if (!IsAdmin(currentUser.Rol?.Nombre))
        {
            var hasPermission = await _context.Cursos
                .AsNoTracking()
                .AnyAsync(c => c.DocenteId == currentUser.Id && c.Activo && (
                    c.GradoId == studentUser.Alumno.GradoId ||
                    c.Inscripciones.Any(i => i.AlumnoId == studentUser.Id && i.Estado == "ACTIVA")));

            if (!hasPermission)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "IDOR_SEMAFORO_FORBIDDEN",
                    message = "No tienes permisos sobre el SemaforoStatusId / AlumnoId solicitado."
                });
            }
        }

        var maxEntrega = studentUser.Alumno.Entregas.OrderByDescending(e => e.FechaEntrega).FirstOrDefault()?.FechaEntrega;
        var maxInscripcion = studentUser.Alumno.Inscripciones.OrderByDescending(i => i.FechaRegistro).FirstOrDefault()?.FechaRegistro;
        var activityMap = new Dictionary<int, DateTime?>
        {
            [studentUser.Id] = maxEntrega ?? maxInscripcion ?? studentUser.FechaCreacion
        };

        var status = _semaforoService.CalculateStatusList(new List<Usuario> { studentUser }, activityMap).FirstOrDefault();
        return Ok(status);
    }

    /// <summary>
    /// Retorna un resumen de métricas del semáforo para el dashboard docente
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(SemaforoSummaryMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics([FromQuery] string? gradeLevel = null)
    {
        var cacheKey = $"semaforo_metrics_{gradeLevel ?? "ALL"}";
        if (_cache.TryGetValue(cacheKey, out SemaforoSummaryMetricsDto? cached) && cached != null)
        {
            return Ok(cached);
        }

        var query = _context.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Entregas)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Inscripciones)
            .Where(u => u.Rol.Nombre == "ALUMNO" && u.Activo)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(gradeLevel))
        {
            var clean = gradeLevel.Contains('°') ? gradeLevel.Substring(0, gradeLevel.IndexOf('°') + 1) : gradeLevel;
            query = query.Where(u => u.Alumno != null && (u.Alumno.Grado.Nombre == clean || u.Alumno.Grado.Nombre == gradeLevel));
        }

        var students = await query.ToListAsync();
        var activityMap = new Dictionary<int, DateTime?>();
        foreach (var s in students)
        {
            var maxEntrega = s.Alumno?.Entregas.OrderByDescending(e => e.FechaEntrega).FirstOrDefault()?.FechaEntrega;
            var maxInscripcion = s.Alumno?.Inscripciones.OrderByDescending(i => i.FechaRegistro).FirstOrDefault()?.FechaRegistro;
            activityMap[s.Id] = maxEntrega ?? maxInscripcion ?? s.FechaCreacion;
        }

        var statusList = _semaforoService.CalculateStatusList(students, activityMap);
        var metrics = _semaforoService.CalculateMetrics(statusList);
        _cache.Set(cacheKey, metrics, TimeSpan.FromSeconds(30));

        return Ok(metrics);
    }

    /// <summary>
    /// Permite al docente publicar una nueva tarea o recurso didáctico para un curso
    /// </summary>
    [HttpPost("contents")]
    [ProducesResponseType(typeof(ContentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateContent([FromBody] CreateContentDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Buscar un curso que coincida con la materia/grado o crear uno automático
        var rawGrade = dto.GradeLevel.Trim();
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

        var isAdmin = IsAdmin(currentUser.Rol?.Nombre);
        Curso? curso = null;
        if (dto.CourseId.HasValue && dto.CourseId.Value > 0)
        {
            curso = await _context.Cursos
                .Include(c => c.Grado)
                .FirstOrDefaultAsync(c => c.Id == dto.CourseId.Value);

            if (curso == null)
            {
                return NotFound(new { message = "El curso especificado no existe." });
            }

            if (!isAdmin && curso.DocenteId != currentUser.Id)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Solo puedes publicar actividades en los cursos que tienes asignados."
                });
            }
        }

        curso ??= await _context.Cursos
            .FirstOrDefaultAsync(c => c.DocenteId == currentUser.Id && c.Nombre == dto.Subject && c.GradoId == grado.Id)
            ?? (isAdmin ? await _context.Cursos.FirstOrDefaultAsync(c => c.Nombre == dto.Subject && c.GradoId == grado.Id) : null)
            ?? await _context.Cursos.FirstOrDefaultAsync(c => c.DocenteId == currentUser.Id);

        if (curso == null)
        {
            var existingGroups = await _context.Cursos
                .Where(c => c.Nombre == dto.Subject.Trim() && c.GradoId == grado.Id)
                .Select(c => c.Grupo)
                .ToListAsync();

            var grupo = "A";
            if (existingGroups.Contains("A"))
            {
                for (char g = 'B'; g <= 'Z'; g++)
                {
                    if (!existingGroups.Contains(g.ToString()))
                    {
                        grupo = g.ToString();
                        break;
                    }
                }
            }

            curso = new Curso
            {
                Nombre = dto.Subject.Trim(),
                GradoId = grado.Id,
                Grupo = grupo,
                DocenteId = currentUser.Id,
                Descripcion = $"Curso de {dto.Subject} para {grado.Nombre}",
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Cursos.Add(curso);
            await _context.SaveChangesAsync();
        }

        var teacherName = !string.IsNullOrWhiteSpace(currentUser.FullName) 
            ? currentUser.FullName 
            : currentUser.Username;

        if (dto.Type.Equals("Assignment", StringComparison.OrdinalIgnoreCase) || 
            dto.Type.Equals("Tarea", StringComparison.OrdinalIgnoreCase) ||
            dto.Type.Equals("Task", StringComparison.OrdinalIgnoreCase))
        {
            var maxScore = dto.MaxScore.HasValue && dto.MaxScore.Value > 0 ? dto.MaxScore.Value : 5.00m;
            var packedDesc = TaskDescriptionMetadata.Pack(dto.Description, dto.FileUrl);
            var (cleanDesc, attachedUrl) = TaskDescriptionMetadata.Unpack(packedDesc);

            var tarea = new Tarea
            {
                CursoId = curso.Id,
                Titulo = dto.Title.Trim(),
                Descripcion = packedDesc,
                FechaPublicacion = DateTime.UtcNow,
                FechaLimite = dto.DueDate.HasValue ? DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(7),
                PuntajeMaximo = maxScore,
                Activo = true
            };
            _context.Tareas.Add(tarea);
            await _context.SaveChangesAsync();

            var detailsMsg = $"Docente {teacherName} ({currentUser.Username}) publicó Tarea #{tarea.Id} '{tarea.Titulo}' en '{curso.Nombre}' ({grado.Nombre}) | Nota Máx: {tarea.PuntajeMaximo:0.##} | Vence: {tarea.FechaLimite:yyyy-MM-dd}" +
                             (!string.IsNullOrWhiteSpace(attachedUrl) ? $" | Guía adjunta: {attachedUrl}" : "");
            RequestLoggingMiddleware.SetAuditContext(
                HttpContext,
                action: "Docente: Publicación de Nueva Tarea Escolar",
                details: detailsMsg);
            await FileLogger.LogAsync("AUDIT", detailsMsg, new
            {
                taskId = tarea.Id,
                title = tarea.Titulo,
                courseId = curso.Id,
                courseName = curso.Nombre,
                maxScore = tarea.PuntajeMaximo,
                dueDate = tarea.FechaLimite,
                hasGuide = !string.IsNullOrWhiteSpace(attachedUrl)
            });

            var resultDto = new ContentDto
            {
                Id = tarea.Id.ToString(),
                RealId = tarea.Id,
                CourseId = curso.Id,
                Title = tarea.Titulo,
                Description = cleanDesc,
                Type = "Assignment",
                Subject = curso.Nombre,
                FileUrl = attachedUrl,
                DueDate = tarea.FechaLimite,
                MaxScore = tarea.PuntajeMaximo,
                GradeLevel = grado.Nombre,
                CreatedByUserId = currentUser.Username,
                CreatedByUserName = teacherName,
                CreatedAt = tarea.FechaPublicacion
            };
            return Ok(resultDto);
        }
        else
        {
            var tipoMaterial = dto.Type.ToUpperInvariant();
            if (tipoMaterial.Contains("VIDEO")) tipoMaterial = "VIDEO";
            else if (tipoMaterial.Contains("PDF") || tipoMaterial.Contains("DOC")) tipoMaterial = "DOCUMENTO";
            else if (tipoMaterial.Contains("IMAGE")) tipoMaterial = "IMAGEN";
            else tipoMaterial = "ENLACE";

            var material = new Material
            {
                CursoId = curso.Id,
                Titulo = dto.Title.Trim(),
                Descripcion = dto.Description.Trim(),
                Tipo = tipoMaterial,
                RecursoUrl = dto.FileUrl ?? string.Empty,
                FechaPublicacion = DateTime.UtcNow,
                Activo = true
            };
            _context.Materiales.Add(material);
            await _context.SaveChangesAsync();

            var detailsMsg = $"Docente {teacherName} ({currentUser.Username}) publicó Material #{material.Id} ({tipoMaterial}) '{material.Titulo}' en '{curso.Nombre}' ({grado.Nombre})";
            RequestLoggingMiddleware.SetAuditContext(
                HttpContext,
                action: "Docente: Publicación de Material de Estudio",
                details: detailsMsg);
            await FileLogger.LogAsync("AUDIT", detailsMsg, new
            {
                materialId = material.Id,
                title = material.Titulo,
                type = tipoMaterial,
                courseId = curso.Id,
                courseName = curso.Nombre,
                resourceUrl = material.RecursoUrl
            });

            var resultDto = new ContentDto
            {
                Id = $"mat-{material.Id}",
                RealId = material.Id,
                CourseId = curso.Id,
                Title = material.Titulo,
                Description = material.Descripcion ?? string.Empty,
                Type = dto.Type,
                Subject = curso.Nombre,
                FileUrl = material.RecursoUrl,
                DueDate = null,
                GradeLevel = grado.Nombre,
                CreatedByUserId = currentUser.Username,
                CreatedByUserName = teacherName,
                CreatedAt = material.FechaPublicacion
            };
            return Ok(resultDto);
        }
    }

    /// <summary>
    /// Permite al docente subir un archivo de recurso educativo
    /// </summary>
    [HttpPost("upload-resource")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("uploads")]
    public async Task<IActionResult> UploadResource(IFormFile file)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Unauthorized();

        var stored = await _fileStorage.SaveFileAsync(
            file,
            "resources",
            currentUser.Id,
            currentUser.Rol?.Nombre ?? "DOCENTE");

        if (!stored.Success)
        {
            return BadRequest(new { message = stored.ErrorMessage });
        }

        return Ok(new
        {
            fileUrl = stored.Url,
            originalName = stored.OriginalName,
            sha256 = stored.Sha256,
            size = stored.Size
        });
    }

    /// <summary>
    /// Consulta las entregas de tareas realizadas por los estudiantes para una tarea específica o todas
    /// </summary>
    [HttpGet("submissions/{assignmentId}")]
    [ProducesResponseType(typeof(List<StudentSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignmentSubmissions(string assignmentId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Unauthorized();

        if (!int.TryParse(assignmentId, out var taskId) || taskId <= 0)
        {
            return BadRequest(new { message = "Identificador de tarea no válido." });
        }

        var tarea = await _context.Tareas
            .AsNoTracking()
            .Include(t => t.Curso)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (tarea == null)
        {
            return NotFound(new { message = "La tarea solicitada no existe." });
        }

        if (!IsAdmin(currentUser.Rol?.Nombre) && tarea.Curso.DocenteId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "IDOR_TASK_FORBIDDEN",
                message = "No tienes autorización para consultar entregas de una tarea que pertenece a otro docente."
            });
        }

        var query = _context.Entregas
            .Include(e => e.Alumno)
                .ThenInclude(a => a.Usuario)
            .Include(e => e.Archivos)
            .Include(e => e.Tarea)
                .ThenInclude(t => t.Curso)
            .Where(e => e.TareaId == taskId)
            .AsQueryable();

        var rawSubmissions = await query
            .OrderByDescending(e => e.FechaEntrega)
            .ToListAsync();

        var submissions = rawSubmissions.Select(e =>
        {
            var latestFile = e.Archivos.OrderByDescending(a => a.FechaSubida).FirstOrDefault();
            return new StudentSubmissionDto
            {
                SubmissionId = e.Id,
                Id = e.Id.ToString(),
                AssignmentId = e.TareaId.ToString(),
                StudentId = e.Alumno.Usuario.Username,
                StudentUsername = e.Alumno.Usuario.Username,
                StudentName = !string.IsNullOrWhiteSpace(e.Alumno.Usuario.FullName) 
                    ? e.Alumno.Usuario.FullName 
                    : e.Alumno.Usuario.Username,
                StudentAvatarUrl = !string.IsNullOrEmpty(e.Alumno.Usuario.AvatarUrl)
                    ? e.Alumno.Usuario.AvatarUrl
                    : $"https://api.dicebear.com/7.x/bottts/svg?seed={e.Alumno.Usuario.Username}",
                FileUrl = latestFile?.RutaArchivo ?? string.Empty,
                OriginalFileName = latestFile?.NombreOriginal ?? "tarea.pdf",
                FileSize = latestFile != null ? (long)latestFile.TamanoBytes : null,
                StudentComment = e.ContenidoTexto,
                SubmittedAt = e.FechaEntrega,
                GradedAt = e.FechaCalificacion,
                Feedback = e.Retroalimentacion,
                Grade = e.Calificacion,
                MaxScore = e.Tarea.PuntajeMaximo,
                DueDate = e.Tarea.FechaLimite,
                IsLate = e.FechaEntrega > e.Tarea.FechaLimite,
                Status = e.Estado
            };
        }).ToList();

        var auditMsg = $"[Ley 1581 / Ley 1098] Consulta de entregas y evidencias de estudiantes ({submissions.Count} entregas para tarea ID {taskId}) por {currentUser.Username}.";
        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            action: "Docente: Consulta de Entregas y Evidencias Escolares",
            details: auditMsg);
        await FileLogger.LogAsync("AUDIT", auditMsg);

        return Ok(submissions);
    }

    /// <summary>
    /// Permite al docente calificar una entrega y dejar retroalimentación al estudiante
    /// POST: /api/teacher/submissions/{submissionId:int}/grade
    /// </summary>
    [HttpPost("submissions/{submissionId:int}/grade")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GradeSubmission(int submissionId, [FromBody] GradeSubmissionDto dto)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Unauthorized();

        var entrega = await _context.Entregas
            .Include(e => e.Alumno)
                .ThenInclude(a => a.Usuario)
            .Include(e => e.Tarea)
                .ThenInclude(t => t.Curso)
            .FirstOrDefaultAsync(e => e.Id == submissionId);

        if (entrega == null)
        {
            return NotFound(new { message = "Entrega no encontrada." });
        }

        if (!IsAdmin(currentUser.Rol?.Nombre) && entrega.Tarea.Curso.DocenteId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Solo puedes calificar entregas de los cursos que tienes asignados."
            });
        }

        var allowedMax = Math.Max(entrega.Tarea.PuntajeMaximo, 5.0m);
        if (dto.Grade < 0 || dto.Grade > allowedMax)
        {
            return BadRequest(new
            {
                message = $"La nota debe estar entre 0 y {entrega.Tarea.PuntajeMaximo:0.##}."
            });
        }

        entrega.Calificacion = dto.Grade;
        entrega.Retroalimentacion = dto.Feedback?.Trim();
        entrega.FechaCalificacion = DateTime.UtcNow;
        entrega.Estado = "CALIFICADA";

        await _context.SaveChangesAsync();

        var studentDisplay = !string.IsNullOrWhiteSpace(entrega.Alumno.Usuario.FullName)
            ? $"{entrega.Alumno.Usuario.FullName} ({entrega.Alumno.Usuario.Username})"
            : entrega.Alumno.Usuario.Username;
        var gradeAuditMsg = $"[Ley 1581 / Ley 1098] Docente {currentUser.Username} calificó entrega #{entrega.Id} de {studentDisplay} en Tarea '{entrega.Tarea.Titulo}' ({entrega.Tarea.Curso.Nombre}) | Nota: {dto.Grade:0.##}/{entrega.Tarea.PuntajeMaximo:0.##}" +
                            (!string.IsNullOrWhiteSpace(entrega.Retroalimentacion) ? $" | Retroalimentación: '{entrega.Retroalimentacion}'" : "");

        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            action: "Docente: Calificación y Retroalimentación de Entrega",
            details: gradeAuditMsg);
        await FileLogger.LogAsync("AUDIT", gradeAuditMsg, new
        {
            submissionId = entrega.Id,
            taskId = entrega.TareaId,
            taskTitle = entrega.Tarea.Titulo,
            studentUsername = entrega.Alumno.Usuario.Username,
            grade = entrega.Calificacion,
            maxScore = entrega.Tarea.PuntajeMaximo,
            gradedBy = currentUser.Username
        });

        return Ok(new
        {
            message = $"¡Entrega calificada exitosamente con {dto.Grade:0.##}! ⭐",
            submissionId = entrega.Id,
            grade = entrega.Calificacion,
            gradedAt = entrega.FechaCalificacion,
            feedback = entrega.Retroalimentacion,
            status = entrega.Estado
        });
    }

    /// <summary>
    /// Permite al docente o admin desmatricular a un estudiante de un curso (Soft Delete / Inactiva)
    /// POST: /api/teacher/courses/{courseId:int}/unenroll
    /// </summary>
    [HttpPost("courses/{courseId:int}/unenroll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnenrollStudent(int courseId, [FromBody] EnrollStudentDto dto)
    {
        if (!await CanManageCourseAsync(courseId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Solo puedes desmatricular estudiantes de los cursos que tienes asignados."
            });
        }

        Alumno? alumno = null;
        if (int.TryParse(dto.StudentId, out var parsedId))
        {
            alumno = await _context.Alumnos.FirstOrDefaultAsync(a => a.UsuarioId == parsedId);
        }

        if (alumno == null)
        {
            var cleanId = dto.StudentId.Trim().ToLower();
            alumno = await _context.Alumnos
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.Usuario.Username.ToLower() == cleanId);
        }

        if (alumno == null) return NotFound(new { message = "Estudiante no encontrado." });

        var inscripcion = await _context.Inscripciones
            .FirstOrDefaultAsync(i => i.CursoId == courseId && i.AlumnoId == alumno.UsuarioId);

        if (inscripcion == null)
        {
            return NotFound(new { message = "El estudiante no está matriculado en este curso." });
        }

        inscripcion.Estado = "INACTIVA";
        await _context.SaveChangesAsync();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Docente";
        _logger.LogInformation("[AUDIT] El Docente {UserId} desmatriculó al Estudiante {AlumnoId} del curso {CursoId}",
            currentUserId, alumno.UsuarioId, courseId);
        await FileLogger.LogAsync("AUDIT", $"El Docente {currentUserId} desmatriculó al Estudiante {alumno.UsuarioId} del curso {courseId}");

        return Ok(new { message = "Estudiante desmatriculado exitosamente del curso." });
    }

    /// <summary>
    /// Permite al docente actualizar datos de un curso
    /// PUT: /api/teacher/courses/{courseId:int}
    /// </summary>
    [HttpPut("courses/{courseId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCourse(int courseId, [FromBody] UpdateCourseDto dto)
    {
        var curso = await _context.Cursos.FindAsync(courseId);
        if (curso == null) return NotFound(new { message = "Curso no encontrado." });

        if (!await CanManageCourseAsync(courseId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Solo puedes editar los cursos que tienes asignados."
            });
        }

        curso.Nombre = dto.Nombre.Trim();
        curso.Grupo = dto.Grupo.Trim().ToUpperInvariant();
        curso.Descripcion = dto.Descripcion?.Trim();

        await _context.SaveChangesAsync();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Docente";
        _logger.LogInformation("[AUDIT] El Docente {UserId} editó/actualizó exitosamente la materia/curso '{NombreCurso}' (ID: {CursoId})",
            currentUserId, curso.Nombre, curso.Id);
        await FileLogger.LogAsync("AUDIT", $"El Docente {currentUserId} actualizó la materia/curso '{curso.Nombre}' (ID: {curso.Id})");

        return Ok(new { message = "Curso actualizado exitosamente.", curso });
    }

    /// <summary>
    /// Retorna métricas clave para el docente: Total estudiantes a cargo, actividades por calificar,
    /// tareas activas y porcentaje de entregas.
    /// GET: /api/teacher/academic-metrics
    /// </summary>
    [HttpGet("academic-metrics")]
    [ProducesResponseType(typeof(TeacherMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherMetrics()
    {
        var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUser = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Username == currentUsername);

        if (currentUser == null) return Unauthorized();

        var isAdmin = IsAdmin(currentUser.Rol?.Nombre);

        var coursesQuery = _context.Cursos
            .Include(c => c.Inscripciones.Where(i => i.Estado == "ACTIVA"))
            .Where(c => c.Activo);

        if (!isAdmin)
        {
            coursesQuery = coursesQuery.Where(c => c.DocenteId == currentUser.Id);
        }

        var courses = await coursesQuery.ToListAsync();
        var courseIds = courses.Select(c => c.Id).ToList();

        // Total estudiantes únicos inscritos en sus cursos
        var totalStudents = courses
            .SelectMany(c => c.Inscripciones)
            .Select(i => i.AlumnoId)
            .Distinct()
            .Count();

        // Tareas del docente
        var tareas = await _context.Tareas
            .Include(t => t.Entregas)
            .Where(t => courseIds.Contains(t.CursoId))
            .ToListAsync();

        var activeTasksCount = tareas.Count(t => t.Activo);

        var allSubmissions = tareas.SelectMany(t => t.Entregas).ToList();
        var pendingGradingCount = allSubmissions.Count(s => !s.Calificacion.HasValue);

        var totalExpectedSubmissions = courses.Sum(c => c.Inscripciones.Count) * (activeTasksCount > 0 ? activeTasksCount : 1);
        decimal submissionRate = 0m;
        if (totalExpectedSubmissions > 0)
        {
            submissionRate = Math.Round((decimal)allSubmissions.Count / totalExpectedSubmissions * 100m, 1);
            if (submissionRate > 100m) submissionRate = 100m;
        }

        return Ok(new TeacherMetricsDto
        {
            TotalStudents = totalStudents,
            PendingGradingCount = pendingGradingCount,
            ActiveTasksCount = activeTasksCount,
            SubmissionRate = submissionRate
        });
    }

    /// <summary>
    /// Consulta el catálogo completo de actividades (tareas y recursos) creadas por el docente con estado y conteo de entregas.
    /// GET: /api/teacher/activities
    /// </summary>
    [HttpGet("activities")]
    [ProducesResponseType(typeof(List<TeacherActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherActivities()
    {
        var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUser = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Username == currentUsername);

        if (currentUser == null) return Unauthorized();

        var isAdmin = IsAdmin(currentUser.Rol?.Nombre);

        var coursesQuery = _context.Cursos
            .Include(c => c.Grado)
            .Include(c => c.Inscripciones.Where(i => i.Estado == "ACTIVA"))
            .Where(c => c.Activo);

        if (!isAdmin)
        {
            coursesQuery = coursesQuery.Where(c => c.DocenteId == currentUser.Id);
        }

        var courses = await coursesQuery.ToListAsync();
        var courseIds = courses.Select(c => c.Id).ToList();
        var coursesMap = courses.ToDictionary(c => c.Id, c => c);

        var tareas = await _context.Tareas
            .Include(t => t.Entregas)
            .Where(t => courseIds.Contains(t.CursoId))
            .OrderByDescending(t => t.FechaPublicacion)
            .ToListAsync();

        var materiales = await _context.Materiales
            .Where(m => courseIds.Contains(m.CursoId))
            .OrderByDescending(m => m.FechaPublicacion)
            .ToListAsync();

        var result = new List<TeacherActivityDto>();

        foreach (var t in tareas)
        {
            var course = coursesMap.GetValueOrDefault(t.CursoId);
            var (cleanDesc, attachedUrl) = TaskDescriptionMetadata.Unpack(t.Descripcion);
            result.Add(new TeacherActivityDto
            {
                Id = t.Id,
                Kind = "Tarea",
                CourseId = t.CursoId,
                CourseName = course?.Nombre ?? "Curso",
                GradeName = course?.Grado.Nombre ?? "",
                Title = t.Titulo,
                Description = cleanDesc,
                CreatedAt = t.FechaPublicacion,
                DueDate = t.FechaLimite,
                MaxScore = t.PuntajeMaximo,
                IsActive = t.Activo,
                SubmissionsCount = t.Entregas.Count,
                PendingGradingCount = t.Entregas.Count(e => !e.Calificacion.HasValue),
                EnrolledStudentsCount = course?.Inscripciones.Count ?? 0,
                ResourceUrl = attachedUrl,
                ResourceType = !string.IsNullOrWhiteSpace(attachedUrl) ? "GUIA" : null
            });
        }

        foreach (var m in materiales)
        {
            var course = coursesMap.GetValueOrDefault(m.CursoId);
            result.Add(new TeacherActivityDto
            {
                Id = m.Id,
                Kind = "Material",
                CourseId = m.CursoId,
                CourseName = course?.Nombre ?? "Curso",
                GradeName = course?.Grado.Nombre ?? "",
                Title = m.Titulo,
                Description = m.Descripcion,
                CreatedAt = m.FechaPublicacion,
                DueDate = null,
                MaxScore = null,
                IsActive = m.Activo,
                SubmissionsCount = 0,
                PendingGradingCount = 0,
                EnrolledStudentsCount = course?.Inscripciones.Count ?? 0,
                ResourceUrl = m.RecursoUrl,
                ResourceType = m.Tipo
            });
        }

        return Ok(result.OrderByDescending(r => r.CreatedAt).ToList());
    }

    /// <summary>
    /// Edita una actividad (tarea o material) existente.
    /// PUT: /api/teacher/activities/{id}?kind=Tarea
    /// </summary>
    [HttpPut("activities/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateActivity(int id, [FromQuery] string kind, [FromBody] UpdateActivityDto dto)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Docente";

        if (string.Equals(kind, "Material", StringComparison.OrdinalIgnoreCase))
        {
            var material = await _context.Materiales.FindAsync(id);
            if (material == null) return NotFound(new { message = "Material no encontrado." });

            if (!await CanManageCourseAsync(material.CursoId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "No puedes editar materiales de cursos ajenos." });
            }

            material.Titulo = dto.Title.Trim();
            material.Descripcion = dto.Description?.Trim();
            if (!string.IsNullOrWhiteSpace(dto.ResourceUrl))
            {
                material.RecursoUrl = dto.ResourceUrl;
            }
            await _context.SaveChangesAsync();

            var msg = $"Docente {actor} actualizó el Material #{material.Id} '{material.Titulo}' (Curso ID:{material.CursoId})";
            RequestLoggingMiddleware.SetAuditContext(HttpContext, action: "Docente: Edición de Material de Estudio", details: msg);
            await FileLogger.LogAsync("AUDIT", msg);

            return Ok(new { message = "Material actualizado exitosamente." });
        }
        else
        {
            var tarea = await _context.Tareas.FindAsync(id);
            if (tarea == null) return NotFound(new { message = "Tarea no encontrada." });

            if (!await CanManageCourseAsync(tarea.CursoId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "No puedes editar tareas de cursos ajenos." });
            }

            if (dto.MaxScore.HasValue && (dto.MaxScore.Value <= 0 || dto.MaxScore.Value > 1000))
            {
                return BadRequest(new { message = "El puntaje máximo debe estar entre 0.01 y 1000." });
            }

            var (_, existingResourceUrl) = TaskDescriptionMetadata.Unpack(tarea.Descripcion);
            var targetResourceUrl = dto.ResourceUrl != null
                ? (string.IsNullOrWhiteSpace(dto.ResourceUrl) ? null : dto.ResourceUrl.Trim())
                : existingResourceUrl;

            tarea.Titulo = dto.Title.Trim();
            tarea.Descripcion = TaskDescriptionMetadata.Pack(dto.Description, targetResourceUrl);
            if (dto.DueDate.HasValue) tarea.FechaLimite = DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc);
            if (dto.MaxScore.HasValue) tarea.PuntajeMaximo = dto.MaxScore.Value;

            await _context.SaveChangesAsync();

            var msg = $"Docente {actor} actualizó la Tarea #{tarea.Id} '{tarea.Titulo}' | Nota Máx: {tarea.PuntajeMaximo:0.##} | Vence: {tarea.FechaLimite:yyyy-MM-dd}";
            RequestLoggingMiddleware.SetAuditContext(HttpContext, action: "Docente: Edición de Tarea Escolar", details: msg);
            await FileLogger.LogAsync("AUDIT", msg);

            return Ok(new { message = "Tarea actualizada exitosamente." });
        }
    }

    /// <summary>
    /// Activa o desactiva (archiva) una actividad sin eliminarla físicamente (Soft Delete).
    /// PATCH: /api/teacher/activities/{id}/toggle-status?kind=Tarea
    /// </summary>
    [HttpPatch("activities/{id:int}/toggle-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActivityStatus(int id, [FromQuery] string kind)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Docente";

        if (string.Equals(kind, "Material", StringComparison.OrdinalIgnoreCase))
        {
            var material = await _context.Materiales.FindAsync(id);
            if (material == null) return NotFound(new { message = "Material no encontrado." });

            if (!await CanManageCourseAsync(material.CursoId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "No puedes archivar materiales de cursos ajenos." });
            }

            material.Activo = !material.Activo;
            await _context.SaveChangesAsync();

            var stateText = material.Activo ? "reactivó" : "archivó";
            var msg = $"Docente {actor} {stateText} el Material #{material.Id} '{material.Titulo}'";
            RequestLoggingMiddleware.SetAuditContext(HttpContext, action: "Docente: Cambio de Estado de Material", details: msg);
            await FileLogger.LogAsync("AUDIT", msg);

            return Ok(new { message = material.Activo ? "Material activado." : "Material archivado.", isActive = material.Activo });
        }
        else
        {
            var tarea = await _context.Tareas.FindAsync(id);
            if (tarea == null) return NotFound(new { message = "Tarea no encontrada." });

            if (!await CanManageCourseAsync(tarea.CursoId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "No puedes archivar tareas de cursos ajenos." });
            }

            tarea.Activo = !tarea.Activo;
            await _context.SaveChangesAsync();

            var stateText = tarea.Activo ? "reactivó" : "archivó";
            var msg = $"Docente {actor} {stateText} la Tarea #{tarea.Id} '{tarea.Titulo}'";
            RequestLoggingMiddleware.SetAuditContext(HttpContext, action: "Docente: Cambio de Estado de Tarea", details: msg);
            await FileLogger.LogAsync("AUDIT", msg);

            return Ok(new { message = tarea.Activo ? "Tarea activada." : "Tarea archivado.", isActive = tarea.Activo });
        }
    }

    /// <summary>
    /// Consulta el libro de calificaciones completo de una materia (estudiantes matriculados x tareas con notas).
    /// GET: /api/teacher/gradebook?courseId={id}
    /// </summary>
    [HttpGet("gradebook")]
    [ProducesResponseType(typeof(GradebookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGradebook([FromQuery] int courseId)
    {
        if (!await CanManageCourseAsync(courseId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No tienes permiso para acceder a este curso." });
        }

        var course = await _context.Cursos
            .AsNoTracking()
            .Include(c => c.Grado)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null) return NotFound(new { message = "Curso no encontrado." });

        // Tareas del curso ordenadas por fecha límite
        var tasks = await _context.Tareas
            .AsNoTracking()
            .Where(t => t.CursoId == courseId && t.Activo)
            .OrderBy(t => t.FechaLimite)
            .Select(t => new GradebookTaskDto
            {
                Id = t.Id,
                Title = t.Titulo,
                MaxScore = t.PuntajeMaximo,
                DueDate = t.FechaLimite
            })
            .ToListAsync();

        var taskIds = tasks.Select(t => t.Id).ToList();

        // Estudiantes matriculados activos
        var enrollments = await _context.Inscripciones
            .AsNoTracking()
            .Include(i => i.Alumno)
                .ThenInclude(a => a.Usuario)
            .Where(i => i.CursoId == courseId && i.Estado == "ACTIVA" && i.Alumno.Usuario.Activo && i.Alumno.Usuario.AnonymizedAt == null)
            .OrderBy(i => i.Alumno.Usuario.Apellido)
            .ThenBy(i => i.Alumno.Usuario.Nombre)
            .ToListAsync();

        // Entregas de estas tareas
        var submissions = await _context.Entregas
            .AsNoTracking()
            .Where(e => taskIds.Contains(e.TareaId))
            .ToListAsync();

        var studentRows = new List<GradebookStudentDto>();
        foreach (var enr in enrollments)
        {
            var studentSubmissions = submissions.Where(e => e.AlumnoId == enr.AlumnoId).ToList();
            var gradesDict = new Dictionary<string, decimal?>();
            var gradedScores = new List<decimal>();

            foreach (var t in tasks)
            {
                var sub = studentSubmissions.FirstOrDefault(s => s.TareaId == t.Id);
                gradesDict[t.Id.ToString()] = sub?.Calificacion;
                if (sub?.Calificacion != null)
                {
                    gradedScores.Add(sub.Calificacion.Value);
                }
            }

            studentRows.Add(new GradebookStudentDto
            {
                StudentId = enr.AlumnoId,
                FullName = enr.Alumno.Usuario.FullName,
                Username = enr.Alumno.Usuario.Username,
                Grades = gradesDict,
                SubmittedCount = studentSubmissions.Count,
                Average = gradedScores.Count > 0 ? Math.Round(gradedScores.Average(), 1) : null
            });
        }

        var result = new GradebookDto
        {
            CourseId = course.Id,
            CourseName = course.Nombre,
            GradeName = course.Grado.Nombre,
            Tasks = tasks,
            Students = studentRows
        };

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Docente";
        await FileLogger.LogAsync("AUDIT",
            $"[Ley 1581 / Ley 1098] Consulta de libro de calificaciones para curso #{courseId} '{course.Nombre}' ({studentRows.Count} estudiantes) por {actor}.");

        return Ok(result);
    }

    /// <summary>
    /// Consulta el progreso histórico y calificaciones de un alumno en las materias del docente.
    /// GET: /api/teacher/students/{studentId}/progress
    /// </summary>
    [HttpGet("students/{studentId:int}/progress")]
    [ProducesResponseType(typeof(List<StudentProgressItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentProgress(int studentId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return Unauthorized();

        var alumno = await _context.Alumnos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UsuarioId == studentId);

        if (alumno == null)
        {
            return NotFound(new { message = "Estudiante no encontrado." });
        }

        var isAdmin = IsAdmin(currentUser.Rol?.Nombre);
        if (!isAdmin)
        {
            var canAccessStudent = await _context.Cursos
                .AsNoTracking()
                .AnyAsync(c => c.DocenteId == currentUser.Id && c.Activo && (
                    c.GradoId == alumno.GradoId ||
                    c.Inscripciones.Any(i => i.AlumnoId == studentId && i.Estado == "ACTIVA")));

            if (!canAccessStudent)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "IDOR_STUDENT_FORBIDDEN",
                    message = "No tienes permisos sobre el AlumnoId solicitado."
                });
            }
        }

        var query = _context.Entregas
            .AsNoTracking()
            .Include(e => e.Tarea)
                .ThenInclude(t => t.Curso)
            .Where(e => e.AlumnoId == studentId);

        if (!isAdmin)
        {
            query = query.Where(e => e.Tarea.Curso.DocenteId == currentUser.Id);
        }

        var progressItems = await query
            .OrderBy(e => e.FechaEntrega)
            .Select(e => new StudentProgressItemDto
            {
                TaskId = e.TareaId,
                TaskTitle = e.Tarea.Titulo,
                CourseName = e.Tarea.Curso.Nombre,
                Grade = e.Calificacion,
                MaxScore = e.Tarea.PuntajeMaximo,
                SubmittedAt = e.FechaEntrega,
                GradedAt = e.FechaCalificacion,
                IsLate = e.FechaEntrega > e.Tarea.FechaLimite
            })
            .ToListAsync();

        var actor = currentUser.Username;
        await FileLogger.LogAsync("AUDIT",
            $"[Ley 1581 / Ley 1098] Consulta de historial de progreso escolar de alumno ID:{studentId} ({progressItems.Count} registros) por {actor}.");

        return Ok(progressItems);
    }
}
