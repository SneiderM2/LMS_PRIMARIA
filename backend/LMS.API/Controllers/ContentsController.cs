using System.Security.Claims;
using LMS.API.Data;
using LMS.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContentsController : ControllerBase
{
    private readonly LMSDbContext _context;

    public ContentsController(LMSDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ContentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContents([FromQuery] string? gradeLevel = null, [FromQuery] string? type = null)
    {
        var result = new List<ContentDto>();

        // Principio de minimización: un estudiante solo ve contenidos de su grado o de
        // los cursos en los que está matriculado (no todo el colegio).
        var role = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);

        int? studentGradeId = null;
        List<int>? enrolledCourseIds = null;

        if (string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized();
            }

            var student = await _context.Alumnos
                .Include(a => a.Inscripciones)
                .FirstOrDefaultAsync(a => a.Usuario.Username.ToLower() == username.ToLower());

            if (student == null)
            {
                return Forbid();
            }

            studentGradeId = student.GradoId;
            enrolledCourseIds = student.Inscripciones
                .Where(i => i.Estado == "ACTIVA")
                .Select(i => i.CursoId)
                .ToList();
        }

        // 1. Tareas
        var tareasQuery = _context.Tareas
            .Include(t => t.Curso)
                .ThenInclude(c => c.Grado)
            .Include(t => t.Curso)
                .ThenInclude(c => c.Docente)
            .Where(t => t.Activo)
            .AsQueryable();

        if (studentGradeId.HasValue)
        {
            tareasQuery = tareasQuery.Where(t =>
                t.Curso.GradoId == studentGradeId.Value
                || (enrolledCourseIds != null && enrolledCourseIds.Contains(t.CursoId)));
        }

        if (!string.IsNullOrWhiteSpace(gradeLevel))
        {
            var clean = gradeLevel.Contains('°') ? gradeLevel.Substring(0, gradeLevel.IndexOf('°') + 1) : gradeLevel;
            tareasQuery = tareasQuery.Where(t => t.Curso.Grado.Nombre == clean || t.Curso.Grado.Nombre == gradeLevel);
        }

        var tareas = await tareasQuery.OrderByDescending(t => t.FechaPublicacion).ToListAsync();
        foreach (var t in tareas)
        {
            var teacherName = !string.IsNullOrWhiteSpace(t.Curso.Docente.FullName) 
                ? t.Curso.Docente.FullName 
                : t.Curso.Docente.Username;

            result.Add(new ContentDto
            {
                Id = t.Id.ToString(),
                RealId = t.Id,
                Title = t.Titulo,
                Description = t.Descripcion ?? string.Empty,
                Type = "Assignment",
                Subject = t.Curso.Nombre,
                DueDate = t.FechaLimite,
                GradeLevel = t.Curso.Grado.Nombre,
                CreatedByUserId = t.Curso.Docente.Username,
                CreatedByUserName = teacherName,
                CreatedAt = t.FechaPublicacion
            });
        }

        // 2. Materiales
        var materialesQuery = _context.Materiales
            .Include(m => m.Curso)
                .ThenInclude(c => c.Grado)
            .Include(m => m.Curso)
                .ThenInclude(c => c.Docente)
            .Where(m => m.Activo)
            .AsQueryable();

        if (studentGradeId.HasValue)
        {
            materialesQuery = materialesQuery.Where(m =>
                m.Curso.GradoId == studentGradeId.Value
                || (enrolledCourseIds != null && enrolledCourseIds.Contains(m.CursoId)));
        }

        if (!string.IsNullOrWhiteSpace(gradeLevel))
        {
            var clean = gradeLevel.Contains('°') ? gradeLevel.Substring(0, gradeLevel.IndexOf('°') + 1) : gradeLevel;
            materialesQuery = materialesQuery.Where(m => m.Curso.Grado.Nombre == clean || m.Curso.Grado.Nombre == gradeLevel);
        }

        var materiales = await materialesQuery.OrderByDescending(m => m.FechaPublicacion).ToListAsync();
        foreach (var m in materiales)
        {
            var teacherName = !string.IsNullOrWhiteSpace(m.Curso.Docente.FullName) 
                ? m.Curso.Docente.FullName 
                : m.Curso.Docente.Username;

            result.Add(new ContentDto
            {
                Id = $"mat-{m.Id}",
                RealId = m.Id,
                Title = m.Titulo,
                Description = m.Descripcion ?? string.Empty,
                Type = m.Tipo == "VIDEO" ? "Video" : "Pdf",
                Subject = m.Curso.Nombre,
                FileUrl = m.RecursoUrl,
                DueDate = null,
                GradeLevel = m.Curso.Grado.Nombre,
                CreatedByUserId = m.Curso.Docente.Username,
                CreatedByUserName = teacherName,
                CreatedAt = m.FechaPublicacion
            });
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            result = result.Where(r => r.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Ok(result.OrderByDescending(r => r.CreatedAt).ToList());
    }
}
