using System.Security.Claims;
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

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Student,ALUMNO")]
public class StudentController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<StudentController> _logger;

    public StudentController(
        LMSDbContext context,
        IFileStorageService fileStorage,
        IOptions<StorageOptions> storageOptions,
        ILogger<StudentController> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Retorna las materias, recursos y tareas asignadas al estudiante
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(List<ContentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentDashboard()
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var student = await _context.Alumnos
            .Include(a => a.Usuario)
            .Include(a => a.Grado)
            .Include(a => a.Inscripciones)
            .FirstOrDefaultAsync(a => a.Usuario.Username.ToLower() == username.ToLower());

        if (student == null)
        {
            return NotFound("Estudiante no encontrado.");
        }

        var studentFullName = !string.IsNullOrWhiteSpace(student.Usuario.FullName) 
            ? student.Usuario.FullName 
            : student.Usuario.Username;

        // Cursos a los que pertenece el alumno (bien sea por grado o por matrícula activa)
        var enrolledCourseIds = student.Inscripciones
            .Where(i => i.Estado == "ACTIVA")
            .Select(i => i.CursoId)
            .ToList();

        var tareas = await _context.Tareas
            .Include(t => t.Curso)
                .ThenInclude(c => c.Docente)
            .Include(t => t.Curso)
                .ThenInclude(c => c.Grado)
            .Include(t => t.Entregas.Where(e => e.AlumnoId == student.UsuarioId))
                .ThenInclude(e => e.Archivos)
            .Where(t => t.Activo && (t.Curso.GradoId == student.GradoId || enrolledCourseIds.Contains(t.CursoId)))
            .OrderByDescending(t => t.FechaPublicacion)
            .ToListAsync();

        var materiales = await _context.Materiales
            .Include(m => m.Curso)
                .ThenInclude(c => c.Docente)
            .Include(m => m.Curso)
                .ThenInclude(c => c.Grado)
            .Where(m => m.Activo && (m.Curso.GradoId == student.GradoId || enrolledCourseIds.Contains(m.CursoId)))
            .OrderByDescending(m => m.FechaPublicacion)
            .ToListAsync();

        var result = new List<ContentDto>();

        foreach (var t in tareas)
        {
            var myEntrega = t.Entregas.FirstOrDefault(e => e.AlumnoId == student.UsuarioId);
            var myArchivo = myEntrega?.Archivos.OrderByDescending(a => a.FechaSubida).FirstOrDefault();
            var teacherName = !string.IsNullOrWhiteSpace(t.Curso.Docente.FullName) 
                ? t.Curso.Docente.FullName 
                : t.Curso.Docente.Username;
            var (cleanDesc, attachedGuideUrl) = TaskDescriptionMetadata.Unpack(t.Descripcion);

            result.Add(new ContentDto
            {
                Id = t.Id.ToString(),
                RealId = t.Id,
                CourseId = t.CursoId,
                Title = t.Titulo,
                Description = cleanDesc,
                Type = "Assignment",
                Subject = t.Curso.Nombre,
                FileUrl = attachedGuideUrl,
                DueDate = t.FechaLimite,
                MaxScore = t.PuntajeMaximo,
                GradeLevel = t.Curso.Grado.Nombre,
                CreatedByUserId = t.Curso.Docente.Username,
                CreatedByUserName = teacherName,
                CreatedAt = t.FechaPublicacion,
                HasSubmitted = myEntrega != null,
                MySubmission = myEntrega == null ? null : new StudentSubmissionDto
                {
                    SubmissionId = myEntrega.Id,
                    Id = myEntrega.Id.ToString(),
                    AssignmentId = t.Id.ToString(),
                    StudentId = student.Usuario.Username,
                    StudentUsername = student.Usuario.Username,
                    StudentName = studentFullName,
                    StudentAvatarUrl = student.Usuario.AvatarUrl,
                    FileUrl = myArchivo?.RutaArchivo ?? string.Empty,
                    OriginalFileName = myArchivo?.NombreOriginal ?? "archivo",
                    FileSize = myArchivo != null ? (long)myArchivo.TamanoBytes : null,
                    StudentComment = myEntrega.ContenidoTexto,
                    SubmittedAt = myEntrega.FechaEntrega,
                    GradedAt = myEntrega.FechaCalificacion,
                    Feedback = myEntrega.Retroalimentacion,
                    Grade = myEntrega.Calificacion,
                    MaxScore = t.PuntajeMaximo,
                    DueDate = t.FechaLimite,
                    IsLate = myEntrega.FechaEntrega > t.FechaLimite,
                    Status = myEntrega.Estado
                }
            });
        }

        foreach (var m in materiales)
        {
            var teacherName = !string.IsNullOrWhiteSpace(m.Curso.Docente.FullName) 
                ? m.Curso.Docente.FullName 
                : m.Curso.Docente.Username;

            result.Add(new ContentDto
            {
                Id = $"mat-{m.Id}",
                RealId = m.Id,
                CourseId = m.CursoId,
                Title = m.Titulo,
                Description = m.Descripcion ?? string.Empty,
                Type = m.Tipo == "VIDEO" ? "Video" : "Pdf",
                Subject = m.Curso.Nombre,
                FileUrl = m.RecursoUrl ?? string.Empty,
                DueDate = null,
                GradeLevel = m.Curso.Grado.Nombre,
                CreatedByUserId = m.Curso.Docente.Username,
                CreatedByUserName = teacherName,
                CreatedAt = m.FechaPublicacion,
                HasSubmitted = false
            });
        }

        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            details: $"Alumno: {studentFullName} ({student.Usuario.Username}) | Grado: {student.Grado.Nombre} | Tareas: {tareas.Count}, Materiales: {materiales.Count}");

        return Ok(result.OrderByDescending(r => r.CreatedAt).ToList());
    }

    /// <summary>
    /// Endpoint para que el alumno suba o adjunte el archivo de su tarea mediante Drag & Drop
    /// </summary>
    [HttpPost("upload-assignment")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("uploads")]
    [ProducesResponseType(typeof(UploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadAssignment([FromForm] UploadAssignmentDto request)
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var student = await _context.Alumnos
            .Include(a => a.Usuario)
            .FirstOrDefaultAsync(a => a.Usuario.Username.ToLower() == username.ToLower());

        if (student == null)
        {
            return NotFound("Estudiante no encontrado.");
        }

        var studentFullName = !string.IsNullOrWhiteSpace(student.Usuario.FullName)
            ? student.Usuario.FullName
            : student.Usuario.Username;

        // 1. Validar la tarea y la pertenencia del estudiante ANTES de guardar el archivo
        //    (evita IDOR y archivos huérfanos).
        var cleanAssignmentId = (request.AssignmentId ?? string.Empty).Replace("task-", string.Empty).Trim();
        if (!int.TryParse(cleanAssignmentId, out var taskId) || taskId <= 0)
        {
            return BadRequest(new { message = "Identificador de tarea no válido." });
        }

        var enrolledCourseIds = await _context.Inscripciones
            .Where(i => i.AlumnoId == student.UsuarioId && i.Estado == "ACTIVA")
            .Select(i => i.CursoId)
            .ToListAsync();

        var tarea = await _context.Tareas
            .Include(t => t.Curso)
            .FirstOrDefaultAsync(t => t.Id == taskId
                                      && t.Activo
                                      && (t.Curso.GradoId == student.GradoId
                                          || enrolledCourseIds.Contains(t.CursoId)));

        if (tarea == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "La tarea no existe, está inactiva o no pertenece a los cursos en los que estás matriculado."
            });
        }

        // 2. Localizar la entrega existente y permitir reenvío limpiando el archivo más antiguo si alcanza el tope.
        var entrega = await _context.Entregas
            .Include(e => e.Archivos)
            .FirstOrDefaultAsync(e => e.TareaId == tarea.Id && e.AlumnoId == student.UsuarioId);

        var maxFiles = Math.Clamp(_storageOptions.MaxFilesPerSubmission, 1, 20);
        if (entrega != null && entrega.Archivos.Count >= maxFiles)
        {
            var oldest = entrega.Archivos.OrderBy(a => a.FechaSubida).First();
            _context.ArchivosEntrega.Remove(oldest);
        }

        // 3. Guardar el archivo en almacenamiento privado con validación de firma real.
        var stored = await _fileStorage.SaveFileAsync(
            request.File,
            "submissions",
            student.UsuarioId,
            "ALUMNO");

        if (!stored.Success)
        {
            return BadRequest(new { message = stored.ErrorMessage });
        }

        var trimmedComment = string.IsNullOrWhiteSpace(request.StudentComment)
            ? null
            : request.StudentComment.Trim();

        if (entrega == null)
        {
            entrega = new Entrega
            {
                TareaId = tarea.Id,
                AlumnoId = student.UsuarioId,
                ContenidoTexto = trimmedComment,
                FechaEntrega = DateTime.UtcNow,
                Estado = "ENVIADA"
            };
            _context.Entregas.Add(entrega);
            await _context.SaveChangesAsync();
        }
        else
        {
            entrega.FechaEntrega = DateTime.UtcNow;
            if (trimmedComment != null)
            {
                entrega.ContenidoTexto = trimmedComment;
            }
            if (entrega.Estado != "CALIFICADA")
            {
                entrega.Estado = "ENVIADA";
            }
        }

        var archivo = new ArchivoEntrega
        {
            EntregaId = entrega.Id,
            NombreOriginal = stored.OriginalName,
            NombreArchivo = Path.GetFileName(stored.StorageKey),
            RutaArchivo = stored.Url,
            TipoMime = stored.ContentType,
            TamanoBytes = (ulong)stored.Size,
            FechaSubida = DateTime.UtcNow
        };
        _context.ArchivosEntrega.Add(archivo);
        await _context.SaveChangesAsync();

        var isLate = entrega.FechaEntrega > tarea.FechaLimite;
        var auditSummary = $"Alumno: {studentFullName} ({student.Usuario.Username}, ID:{student.UsuarioId}) | Tarea #{tarea.Id}: '{tarea.Titulo}' (Curso: {tarea.Curso.Nombre}) | Archivo: '{stored.OriginalName}' ({stored.Size / 1024.0:F1} KB) | Puntualidad: {(isLate ? "Entrega Tardía" : "A Tiempo")}" +
                           (trimmedComment != null ? $" | Nota del alumno: '{trimmedComment}'" : "");

        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            action: "Estudiante: Entrega de Evidencia / Archivo de Tarea",
            details: auditSummary);

        _logger.LogInformation("[AUDIT] {Summary}", auditSummary);
        await FileLogger.LogAsync("AUDIT", $"Estudiante {studentFullName} ({student.Usuario.Username}) entregó tarea #{tarea.Id} '{tarea.Titulo}'", new
        {
            studentId = student.UsuarioId,
            studentUsername = student.Usuario.Username,
            taskId = tarea.Id,
            taskTitle = tarea.Titulo,
            courseId = tarea.CursoId,
            courseName = tarea.Curso.Nombre,
            fileName = stored.OriginalName,
            fileSizeKb = Math.Round(stored.Size / 1024.0, 2),
            isLate,
            hasComment = trimmedComment != null
        });

        return Ok(new UploadResultDto
        {
            Success = true,
            Message = "¡Felicidades! Tu tarea ha sido entregada con éxito. 🎉⭐",
            Submission = new StudentSubmissionDto
            {
                SubmissionId = entrega.Id,
                Id = entrega.Id.ToString(),
                AssignmentId = tarea.Id.ToString(),
                StudentId = student.Usuario.Username,
                StudentUsername = student.Usuario.Username,
                StudentName = studentFullName,
                StudentAvatarUrl = student.Usuario.AvatarUrl,
                FileUrl = stored.Url,
                OriginalFileName = stored.OriginalName,
                FileSize = stored.Size,
                StudentComment = entrega.ContenidoTexto,
                SubmittedAt = entrega.FechaEntrega,
                GradedAt = entrega.FechaCalificacion,
                Feedback = entrega.Retroalimentacion,
                Grade = entrega.Calificacion,
                MaxScore = tarea.PuntajeMaximo,
                DueDate = tarea.FechaLimite,
                IsLate = isLate,
                Status = entrega.Estado
            }
        });
    }

    /// <summary>
    /// Retorna métricas clave para el estudiante: Clases inscritas, promedio acumulado,
    /// actividades pendientes y porcentaje de progreso.
    /// GET: /api/student/metrics
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(StudentMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentMetrics()
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var student = await _context.Alumnos
            .Include(a => a.Inscripciones)
            .FirstOrDefaultAsync(a => a.Usuario.Username.ToLower() == username.ToLower());

        if (student == null) return NotFound("Estudiante no encontrado.");

        var activeEnrolledCourseIds = student.Inscripciones
            .Where(i => i.Estado == "ACTIVA")
            .Select(i => i.CursoId)
            .ToList();

        var enrolledCoursesCount = activeEnrolledCourseIds.Count;

        // Tareas en sus cursos activos o de su grado
        var relevantTasks = await _context.Tareas
            .Include(t => t.Entregas.Where(e => e.AlumnoId == student.UsuarioId))
            .Where(t => t.Activo && (activeEnrolledCourseIds.Contains(t.CursoId) || t.Curso.GradoId == student.GradoId))
            .ToListAsync();

        var totalActivities = relevantTasks.Count;
        var submittedTasks = relevantTasks.Count(t => t.Entregas.Any());
        var pendingActivities = totalActivities - submittedTasks;

        // Promedio acumulado de entregas calificadas (escala 5.0 estándar)
        var gradedSubmissions = await _context.Entregas
            .Where(e => e.AlumnoId == student.UsuarioId && e.Calificacion.HasValue)
            .Select(e => e.Calificacion!.Value)
            .ToListAsync();

        decimal gpa = 5.0m;
        if (gradedSubmissions.Any())
        {
            var rawAvg = gradedSubmissions.Average();
            // Si la nota fue calificada sobre 100, normalizar a escala 5.0 si aplica
            gpa = rawAvg > 5.0m ? Math.Round(rawAvg / 20.0m, 1) : Math.Round(rawAvg, 1);
        }

        decimal progress = totalActivities > 0
            ? Math.Round((decimal)submittedTasks / totalActivities * 100m, 1)
            : 100m;

        return Ok(new StudentMetricsDto
        {
            EnrolledCoursesCount = enrolledCoursesCount,
            CumulativeGpa = gpa,
            PendingActivitiesCount = pendingActivities < 0 ? 0 : pendingActivities,
            ProgressPercentage = progress
        });
    }

    /// <summary>
    /// Consulta el catálogo de clases disponibles y las clases activas del estudiante.
    /// GET: /api/student/courses
    /// </summary>
    [HttpGet("courses")]
    [ProducesResponseType(typeof(StudentCoursesCatalogDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentCourses()
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var student = await _context.Alumnos
            .Include(a => a.Inscripciones)
            .FirstOrDefaultAsync(a => a.Usuario.Username.ToLower() == username.ToLower());

        if (student == null) return NotFound("Estudiante no encontrado.");

        var allCourses = await _context.Cursos
            .Include(c => c.Grado)
            .Include(c => c.Docente)
            .Where(c => c.Activo && c.GradoId == student.GradoId)
            .ToListAsync();

        var enrollmentsMap = student.Inscripciones
            .ToDictionary(i => i.CursoId, i => i);

        var myActiveList = new List<StudentCourseItemDto>();
        var availableList = new List<StudentCourseItemDto>();

        foreach (var c in allCourses)
        {
            var teacherName = !string.IsNullOrWhiteSpace(c.Docente.FullName) ? c.Docente.FullName : c.Docente.Username;
            var isEnrolled = enrollmentsMap.TryGetValue(c.Id, out var enrollment);
            var isActivelyEnrolled = isEnrolled && enrollment!.Estado == "ACTIVA";

            var dto = new StudentCourseItemDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Grado = c.Grado.Nombre,
                Grupo = c.Grupo,
                DocenteNombre = teacherName,
                Descripcion = c.Descripcion,
                IsEnrolled = isActivelyEnrolled,
                EnrollmentStatus = isEnrolled ? enrollment!.Estado : "NO_INSCRITO",
                FechaInscripcion = isEnrolled ? enrollment!.FechaRegistro : null
            };

            if (isActivelyEnrolled)
            {
                myActiveList.Add(dto);
            }
            else
            {
                availableList.Add(dto);
            }
        }

        return Ok(new StudentCoursesCatalogDto
        {
            MyActiveCourses = myActiveList,
            AvailableCourses = availableList
        });
    }

    /// <summary>
    /// Inscribe al estudiante en una nueva clase.
    /// POST: /api/student/courses/{courseId}/enroll
    /// </summary>
    [HttpPost("courses/{courseId:int}/enroll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnrollInCourse(int courseId)
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var student = await _context.Alumnos
            .Include(a => a.Inscripciones)
            .FirstOrDefaultAsync(a => a.Usuario.Username.ToLower() == username.ToLower());

        if (student == null) return NotFound("Estudiante no encontrado.");

        var course = await _context.Cursos.FirstOrDefaultAsync(c => c.Id == courseId && c.Activo);
        if (course == null) return NotFound(new { message = "La clase no existe o está inactiva." });

        var existingEnrollment = student.Inscripciones.FirstOrDefault(i => i.CursoId == courseId);
        if (existingEnrollment != null)
        {
            existingEnrollment.Estado = "ACTIVA";
            existingEnrollment.FechaRegistro = DateTime.UtcNow;
        }
        else
        {
            var newEnrollment = new Inscripcion
            {
                CursoId = courseId,
                AlumnoId = student.UsuarioId,
                FechaRegistro = DateTime.UtcNow,
                Estado = "ACTIVA"
            };
            _context.Inscripciones.Add(newEnrollment);
        }

        await _context.SaveChangesAsync();
        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            action: "Estudiante: Inscripción Voluntaria en Curso",
            details: $"Alumno ID:{student.UsuarioId} ({username}) se inscribió al curso #{course.Id} '{course.Nombre}'");
        await FileLogger.LogAsync("AUDIT", $"Estudiante {username} (ID:{student.UsuarioId}) se inscribió al curso '{course.Nombre}' (ID:{course.Id})");
        return Ok(new { message = $"¡Te has inscrito exitosamente a {course.Nombre}! 🎒✨" });
    }

    /// <summary>
    /// Desactiva / da de baja una clase de la lista activa del estudiante (Soft Delete).
    /// POST: /api/student/courses/{courseId}/withdraw
    /// </summary>
    [HttpPost("courses/{courseId:int}/withdraw")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WithdrawFromCourse(int courseId)
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var student = await _context.Alumnos
            .Include(a => a.Inscripciones)
            .FirstOrDefaultAsync(a => a.Usuario.Username.ToLower() == username.ToLower());

        if (student == null) return NotFound("Estudiante no encontrado.");

        var enrollment = student.Inscripciones.FirstOrDefault(i => i.CursoId == courseId && i.Estado == "ACTIVA");
        if (enrollment == null)
        {
            return NotFound(new { message = "No tienes una inscripción activa en esta clase." });
        }

        // Desactivar lógicamente para preservar historial
        enrollment.Estado = "INACTIVA";
        await _context.SaveChangesAsync();

        RequestLoggingMiddleware.SetAuditContext(
            HttpContext,
            action: "Estudiante: Retiro Voluntario de Curso",
            details: $"Alumno ID:{student.UsuarioId} ({username}) retiró el curso #{courseId} de su lista activa");
        await FileLogger.LogAsync("AUDIT", $"Estudiante {username} (ID:{student.UsuarioId}) retiró el curso ID:{courseId}");

        return Ok(new { message = "Has retirado la clase de tu lista activa exitosamente." });
    }
}
