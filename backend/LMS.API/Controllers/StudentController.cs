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
[Authorize(Roles = "Student,ALUMNO")]
public class StudentController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public StudentController(LMSDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
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
            var myArchivo = myEntrega?.Archivos.FirstOrDefault();
            var teacherName = !string.IsNullOrWhiteSpace(t.Curso.Docente.FullName) 
                ? t.Curso.Docente.FullName 
                : t.Curso.Docente.Username;

            result.Add(new ContentDto
            {
                Id = Guid.NewGuid(),
                Title = t.Titulo,
                Description = t.Descripcion ?? string.Empty,
                Type = "Assignment",
                Subject = t.Curso.Nombre,
                DueDate = t.FechaLimite,
                GradeLevel = t.Curso.Grado.Nombre,
                CreatedByUserId = t.Curso.Docente.Username,
                CreatedByUserName = teacherName,
                CreatedAt = t.FechaPublicacion,
                HasSubmitted = myEntrega != null,
                MySubmission = myEntrega == null ? null : new StudentSubmissionDto
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = Guid.Empty,
                    StudentId = student.Usuario.Username,
                    StudentName = studentFullName,
                    FileUrl = myArchivo?.RutaArchivo ?? string.Empty,
                    OriginalFileName = myArchivo?.NombreOriginal ?? "archivo",
                    SubmittedAt = myEntrega.FechaEntrega,
                    Feedback = myEntrega.Retroalimentacion,
                    Grade = myEntrega.Calificacion
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
                Id = Guid.NewGuid(),
                Title = m.Titulo,
                Description = m.Descripcion ?? string.Empty,
                Type = m.Tipo == "VIDEO" ? "Video" : "Pdf",
                Subject = m.Curso.Nombre,
                FileUrl = m.RecursoUrl,
                DueDate = null,
                GradeLevel = m.Curso.Grado.Nombre,
                CreatedByUserId = m.Curso.Docente.Username,
                CreatedByUserName = teacherName,
                CreatedAt = m.FechaPublicacion,
                HasSubmitted = false
            });
        }

        return Ok(result.OrderByDescending(r => r.CreatedAt).ToList());
    }

    /// <summary>
    /// Endpoint para que el alumno suba o adjunte el archivo de su tarea mediante Drag & Drop
    /// </summary>
    [HttpPost("upload-assignment")]
    [Consumes("multipart/form-data")]
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

        // Guardar archivo físico
        var (success, fileUrl, originalName, error) = await _fileStorage.SaveFileAsync(request.File, "submissions");
        if (!success)
        {
            return BadRequest(new { message = error });
        }

        // Buscar la tarea activa
        var tarea = await _context.Tareas.FirstOrDefaultAsync(t => t.Activo)
                    ?? await _context.Tareas.OrderByDescending(t => t.Id).FirstOrDefaultAsync();

        if (tarea == null)
        {
            return BadRequest(new { message = "No hay tareas activas disponibles para entrega en este momento." });
        }

        var entrega = await _context.Entregas
            .Include(e => e.Archivos)
            .FirstOrDefaultAsync(e => e.TareaId == tarea.Id && e.AlumnoId == student.UsuarioId);

        if (entrega == null)
        {
            entrega = new Entrega
            {
                TareaId = tarea.Id,
                AlumnoId = student.UsuarioId,
                FechaEntrega = DateTime.UtcNow,
                Estado = "ENVIADA"
            };
            _context.Entregas.Add(entrega);
            await _context.SaveChangesAsync();
        }
        else
        {
            entrega.FechaEntrega = DateTime.UtcNow;
            entrega.Estado = "ENVIADA";
        }

        var archivo = new ArchivoEntrega
        {
            EntregaId = entrega.Id,
            NombreOriginal = originalName,
            NombreArchivo = Path.GetFileName(fileUrl),
            RutaArchivo = fileUrl,
            TipoMime = request.File.ContentType ?? "application/octet-stream",
            TamanoBytes = (ulong)request.File.Length,
            FechaSubida = DateTime.UtcNow
        };
        _context.ArchivosEntrega.Add(archivo);
        await _context.SaveChangesAsync();

        return Ok(new UploadResultDto
        {
            Success = true,
            Message = "¡Felicidades! Tu tarea ha sido entregada con éxito. 🎉⭐",
            Submission = new StudentSubmissionDto
            {
                Id = Guid.NewGuid(),
                AssignmentId = Guid.Empty,
                StudentId = student.Usuario.Username,
                StudentName = studentFullName,
                FileUrl = fileUrl,
                OriginalFileName = originalName,
                SubmittedAt = entrega.FechaEntrega
            }
        });
    }
}
