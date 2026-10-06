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

        if (dto.Type.Equals("Assignment", StringComparison.OrdinalIgnoreCase) || 
            dto.Type.Equals("Tarea", StringComparison.OrdinalIgnoreCase) ||
            dto.Type.Equals("Task", StringComparison.OrdinalIgnoreCase))
        {
            var tarea = new Tarea
            {
                CursoId = curso.Id,
                Titulo = dto.Title.Trim(),
                Descripcion = dto.Description.Trim(),
                FechaPublicacion = DateTime.UtcNow,
                FechaLimite = dto.DueDate.HasValue ? DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(7),
                PuntajeMaximo = 100.00m,
                Activo = true
            };
            _context.Tareas.Add(tarea);
            await _context.SaveChangesAsync();

            var resultDto = new ContentDto
            {
                Id = tarea.Id.ToString(),
                RealId = tarea.Id,
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
                Id = $"mat-{material.Id}",
                RealId = material.Id,
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
    /// Consulta las entregas de tareas realizadas por los estudiantes para una tarea específica o todas
    /// </summary>
    [HttpGet("submissions/{assignmentId}")]
    [ProducesResponseType(typeof(List<StudentSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignmentSubmissions(string assignmentId)
    {
        var query = _context.Entregas
            .Include(e => e.Alumno)
                .ThenInclude(a => a.Usuario)
            .Include(e => e.Archivos)
            .Include(e => e.Tarea)
            .AsQueryable();

        if (int.TryParse(assignmentId, out var taskId) && taskId > 0)
        {
            query = query.Where(e => e.TareaId == taskId);
        }

        var submissions = await query
            .OrderByDescending(e => e.FechaEntrega)
            .Select(e => new StudentSubmissionDto
            {
                SubmissionId = e.Id,
                Id = e.Id.ToString(),
                AssignmentId = e.TareaId.ToString(),
                StudentId = e.Alumno.Usuario.Username,
                StudentName = !string.IsNullOrWhiteSpace(e.Alumno.Usuario.FullName) 
                    ? e.Alumno.Usuario.FullName 
                    : e.Alumno.Usuario.Username,
                StudentAvatarUrl = e.Alumno.Usuario.AvatarUrl,
                FileUrl = e.Archivos.FirstOrDefault() != null ? e.Archivos.First().RutaArchivo : string.Empty,
                OriginalFileName = e.Archivos.FirstOrDefault() != null ? e.Archivos.First().NombreOriginal : "tarea.pdf",
                SubmittedAt = e.FechaEntrega,
                Feedback = e.Retroalimentacion,
                Grade = e.Calificacion,
                Status = e.Estado
            })
            .ToListAsync();

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
        var entrega = await _context.Entregas
            .Include(e => e.Alumno)
                .ThenInclude(a => a.Usuario)
            .Include(e => e.Tarea)
            .FirstOrDefaultAsync(e => e.Id == submissionId);

        if (entrega == null)
        {
            return NotFound(new { message = "Entrega no encontrada." });
        }

        entrega.Calificacion = dto.Grade;
        entrega.Retroalimentacion = dto.Feedback?.Trim();
        entrega.FechaCalificacion = DateTime.UtcNow;
        entrega.Estado = "CALIFICADA";

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"¡Entrega calificada exitosamente con {dto.Grade}! ⭐",
            submissionId = entrega.Id,
            grade = entrega.Calificacion,
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

        curso.Nombre = dto.Nombre.Trim();
        curso.Grupo = dto.Grupo.Trim().ToUpperInvariant();
        curso.Descripcion = dto.Descripcion?.Trim();

        await _context.SaveChangesAsync();
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

        var isTeacher = currentUser.Rol.Nombre == "DOCENTE";

        var coursesQuery = _context.Cursos
            .Include(c => c.Inscripciones.Where(i => i.Estado == "ACTIVA"))
            .Where(c => c.Activo);

        if (isTeacher)
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

        var isTeacher = currentUser.Rol.Nombre == "DOCENTE";

        var coursesQuery = _context.Cursos
            .Include(c => c.Grado)
            .Where(c => c.Activo);

        if (isTeacher)
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
            result.Add(new TeacherActivityDto
            {
                Id = t.Id,
                Kind = "Tarea",
                CourseId = t.CursoId,
                CourseName = course?.Nombre ?? "Curso",
                GradeName = course?.Grado.Nombre ?? "",
                Title = t.Titulo,
                Description = t.Descripcion,
                CreatedAt = t.FechaPublicacion,
                DueDate = t.FechaLimite,
                MaxScore = t.PuntajeMaximo,
                IsActive = t.Activo,
                SubmissionsCount = t.Entregas.Count,
                PendingGradingCount = t.Entregas.Count(e => !e.Calificacion.HasValue)
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
        if (string.Equals(kind, "Material", StringComparison.OrdinalIgnoreCase))
        {
            var material = await _context.Materiales.FindAsync(id);
            if (material == null) return NotFound(new { message = "Material no encontrado." });

            material.Titulo = dto.Title.Trim();
            material.Descripcion = dto.Description?.Trim();
            if (!string.IsNullOrWhiteSpace(dto.ResourceUrl))
            {
                material.RecursoUrl = dto.ResourceUrl;
            }
            await _context.SaveChangesAsync();
            return Ok(new { message = "Material actualizado exitosamente." });
        }
        else
        {
            var tarea = await _context.Tareas.FindAsync(id);
            if (tarea == null) return NotFound(new { message = "Tarea no encontrada." });

            tarea.Titulo = dto.Title.Trim();
            tarea.Descripcion = dto.Description?.Trim();
            if (dto.DueDate.HasValue) tarea.FechaLimite = DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc);
            if (dto.MaxScore.HasValue) tarea.PuntajeMaximo = dto.MaxScore.Value;

            await _context.SaveChangesAsync();
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
        if (string.Equals(kind, "Material", StringComparison.OrdinalIgnoreCase))
        {
            var material = await _context.Materiales.FindAsync(id);
            if (material == null) return NotFound(new { message = "Material no encontrado." });

            material.Activo = !material.Activo;
            await _context.SaveChangesAsync();
            return Ok(new { message = material.Activo ? "Material activado." : "Material archivado.", isActive = material.Activo });
        }
        else
        {
            var tarea = await _context.Tareas.FindAsync(id);
            if (tarea == null) return NotFound(new { message = "Tarea no encontrada." });

            tarea.Activo = !tarea.Activo;
            await _context.SaveChangesAsync();
            return Ok(new { message = tarea.Activo ? "Tarea activada." : "Tarea archivada.", isActive = tarea.Activo });
        }
    }
}
