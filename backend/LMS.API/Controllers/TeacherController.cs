using System.Security.Claims;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Teacher,ADMINISTRADOR,DOCENTE")]
public class TeacherController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly ISemaforoService _semaforoService;
    private readonly IFileStorageService _fileStorage;

    public TeacherController(
        LMSDbContext context,
        ISemaforoService semaforoService,
        IFileStorageService fileStorage)
    {
        _context = context;
        _semaforoService = semaforoService;
        _fileStorage = fileStorage;
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
        var grado = await _context.Grados.FirstOrDefaultAsync(g => g.Nombre == gradeKey || g.Nombre == rawGrade)
                    ?? await _context.Grados.FirstOrDefaultAsync(g => g.Nombre.StartsWith("1"))
                    ?? await _context.Grados.FirstOrDefaultAsync();

        if (grado == null)
        {
            grado = new Grado { Nombre = gradeKey, Descripcion = $"{gradeKey} de primaria" };
            _context.Grados.Add(grado);
            await _context.SaveChangesAsync();
        }

        var password = string.IsNullOrWhiteSpace(dto.Password) ? "123456" : dto.Password.Trim();
        var parts = dto.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = parts.Length > 0 ? parts[0] : cleanUsername;
        var lastName = parts.Length > 1 ? parts[1] : "";
        var avatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(cleanUsername)}";

        var nuevoUsuario = new Usuario
        {
            Username = cleanUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RolId = alumnoRol.Id,
            Nombre = firstName,
            Apellido = lastName,
            AvatarUrl = avatarUrl,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        _context.Usuarios.Add(nuevoUsuario);
        await _context.SaveChangesAsync();

        var alumno = new Alumno
        {
            UsuarioId = nuevoUsuario.Id,
            GradoId = grado.Id
        };
        _context.Alumnos.Add(alumno);

        await _context.SaveChangesAsync();

        var resultDto = new TeacherStudentDto
        {
            Id = nuevoUsuario.Id,
            Username = nuevoUsuario.Username,
            FullName = nuevoUsuario.FullName,
            Grade = grado.Nombre,
            AvatarUrl = avatarUrl,
            CreatedAt = nuevoUsuario.FechaCreacion,
            EnrolledCourses = new List<EnrolledCourseSummaryDto>()
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

        // Si es Docente (no Admin), filtrar solo sus cursos asignados
        if (currentUser != null && currentUser.Rol.Nombre == "DOCENTE")
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
        var grado = await _context.Grados.FirstOrDefaultAsync(g => g.Nombre == gradeKey || g.Nombre == rawGrade)
                    ?? await _context.Grados.FirstOrDefaultAsync();

        if (grado == null)
        {
            grado = new Grado { Nombre = gradeKey, Descripcion = $"{gradeKey} de primaria" };
            _context.Grados.Add(grado);
            await _context.SaveChangesAsync();
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
    /// Retorna la lista de estudiantes con su cálculo de estado de semáforo (Verde, Amarillo, Rojo)
    /// </summary>
    [HttpGet("students-status")]
    [ProducesResponseType(typeof(List<StudentStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentsStatus([FromQuery] string? gradeLevel = null)
    {
        var query = _context.Usuarios
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
        return Ok(statusList);
    }

    /// <summary>
    /// Retorna un resumen de métricas del semáforo para el dashboard docente
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(SemaforoSummaryMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics([FromQuery] string? gradeLevel = null)
    {
        var query = _context.Usuarios
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

        var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUser = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Username == currentUsername);

        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Buscar un curso que coincida con la materia/grado o crear uno automático
        var rawGrade = dto.GradeLevel.Trim();
        var gradeKey = rawGrade.Contains('°') ? rawGrade.Substring(0, rawGrade.IndexOf('°') + 1) : rawGrade;
        var grado = await _context.Grados.FirstOrDefaultAsync(g => g.Nombre == gradeKey || g.Nombre == rawGrade)
                    ?? await _context.Grados.FirstOrDefaultAsync();

        if (grado == null)
        {
            grado = new Grado { Nombre = gradeKey, Descripcion = $"{gradeKey} de primaria" };
            _context.Grados.Add(grado);
            await _context.SaveChangesAsync();
        }

        var curso = await _context.Cursos
            .FirstOrDefaultAsync(c => c.DocenteId == currentUser.Id && c.Nombre == dto.Subject && c.GradoId == grado.Id)
            ?? await _context.Cursos.FirstOrDefaultAsync(c => c.DocenteId == currentUser.Id);

        if (curso == null)
        {
            curso = new Curso
            {
                Nombre = dto.Subject.Trim(),
                GradoId = grado.Id,
                Grupo = "A",
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

        if (dto.Type.Equals("Assignment", StringComparison.OrdinalIgnoreCase) || dto.Type.Equals("Tarea", StringComparison.OrdinalIgnoreCase))
        {
            var tarea = new Tarea
            {
                CursoId = curso.Id,
                Titulo = dto.Title.Trim(),
                Descripcion = dto.Description.Trim(),
                FechaPublicacion = DateTime.UtcNow,
                FechaLimite = dto.DueDate ?? DateTime.UtcNow.AddDays(7),
                PuntajeMaximo = 100.00m,
                Activo = true
            };
            _context.Tareas.Add(tarea);
            await _context.SaveChangesAsync();

            var resultDto = new ContentDto
            {
                Id = Guid.NewGuid(),
                Title = tarea.Titulo,
                Description = tarea.Descripcion ?? string.Empty,
                Type = "Assignment",
                Subject = curso.Nombre,
                FileUrl = dto.FileUrl,
                DueDate = tarea.FechaLimite,
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

            var resultDto = new ContentDto
            {
                Id = Guid.NewGuid(),
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
    public async Task<IActionResult> UploadResource(IFormFile file)
    {
        var (success, fileUrl, originalName, error) = await _fileStorage.SaveFileAsync(file, "resources");
        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new { fileUrl, originalName });
    }

    /// <summary>
    /// Consulta las entregas de tareas realizadas por los estudiantes
    /// </summary>
    [HttpGet("submissions/{assignmentId}")]
    [ProducesResponseType(typeof(List<StudentSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignmentSubmissions(string assignmentId)
    {
        var submissions = await _context.Entregas
            .Include(e => e.Alumno)
                .ThenInclude(a => a.Usuario)
            .Include(e => e.Archivos)
            .OrderByDescending(e => e.FechaEntrega)
            .Select(e => new StudentSubmissionDto
            {
                Id = Guid.NewGuid(),
                AssignmentId = Guid.Empty,
                StudentId = e.Alumno.Usuario.Username,
                StudentName = !string.IsNullOrWhiteSpace(e.Alumno.Usuario.FullName) 
                    ? e.Alumno.Usuario.FullName 
                    : e.Alumno.Usuario.Username,
                FileUrl = e.Archivos.FirstOrDefault() != null ? e.Archivos.First().RutaArchivo : string.Empty,
                OriginalFileName = e.Archivos.FirstOrDefault() != null ? e.Archivos.First().NombreOriginal : "tarea.pdf",
                SubmittedAt = e.FechaEntrega,
                Feedback = e.Retroalimentacion,
                Grade = e.Calificacion
            })
            .ToListAsync();

        return Ok(submissions);
    }
}
