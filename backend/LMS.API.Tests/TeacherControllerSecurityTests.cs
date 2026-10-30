using System.Security.Claims;
using LMS.API.Configuration;
using LMS.API.Controllers;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LMS.API.Tests;

public class TeacherControllerSecurityTests
{
    private static readonly string TestEvidenceKey = string.Concat("runtime-evidence-", new string('t', 24), "-2026!");

    private static LMSDbContext CreateSeededDb()
    {
        var options = new DbContextOptionsBuilder<LMSDbContext>()
            .UseInMemoryDatabase(databaseName: $"TeacherTestDb_{Guid.NewGuid():N}")
            .Options;
        var db = new LMSDbContext(options);

        db.Roles.AddRange(
            new Rol { Id = 1, Nombre = "ADMINISTRADOR" },
            new Rol { Id = 2, Nombre = "DOCENTE" },
            new Rol { Id = 3, Nombre = "ALUMNO" }
        );
        db.Grados.AddRange(
            new Grado { Id = 1, Nombre = "1°" },
            new Grado { Id = 2, Nombre = "2°" }
        );

        // Docente A (ID = 100) y Docente B (ID = 200)
        db.Usuarios.AddRange(
            new Usuario { Id = 100, RolId = 2, Username = "docente.a", Nombre = "Ana", Apellido = "Lopez", PasswordHash = "h", Activo = true },
            new Usuario { Id = 200, RolId = 2, Username = "docente.b", Nombre = "Beto", Apellido = "Mora", PasswordHash = "h", Activo = true },
            new Usuario { Id = 300, RolId = 3, Username = "alumno.uno", Nombre = "Pepito", Apellido = "Perez", PasswordHash = "h", Activo = true }
        );
        db.Alumnos.Add(new Alumno { UsuarioId = 300, GradoId = 1 });

        // Curso 1 pertenece a Docente A; Curso 2 pertenece a Docente B
        db.Cursos.AddRange(
            new Curso { Id = 1, Nombre = "Ciencias Naturales", GradoId = 1, Grupo = "A", DocenteId = 100, Activo = true },
            new Curso { Id = 2, Nombre = "Matematicas", GradoId = 1, Grupo = "A", DocenteId = 200, Activo = true }
        );

        // Tarea 10 en Curso 2 (del Docente B) y Entrega 50 en Tarea 10
        db.Tareas.Add(new Tarea
        {
            Id = 10,
            CursoId = 2,
            Titulo = "Tarea de Matematicas Docente B",
            FechaLimite = DateTime.UtcNow.AddDays(3),
            PuntajeMaximo = 5.0m,
            Activo = true
        });
        db.Entregas.Add(new Entrega
        {
            Id = 50,
            TareaId = 10,
            AlumnoId = 300,
            Estado = "ENVIADA",
            FechaEntrega = DateTime.UtcNow
        });

        db.SaveChanges();
        return db;
    }

    private static TeacherController CreateController(LMSDbContext db, string username, string role = "Teacher")
    {
        var privacyOptions = Options.Create(new PrivacyOptions
        {
            PolicyVersion = "2026-10-19",
            EvidenceHmacKey = TestEvidenceKey
        });
        var consent = new ConsentService(privacyOptions);
        var semaforo = new SemaforoService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var storage = new NoOpFileStorageService();

        var controller = new TeacherController(
            db,
            semaforo,
            storage,
            consent,
            cache,
            NullLogger<TeacherController>.Instance);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Role, role)
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Fact]
    public async Task CreateContent_InOwnCourse_ReturnsOkAndPersistsTask()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "docente.a");

        var result = await controller.CreateContent(new CreateContentDto
        {
            Title = "Experimento de Plantas",
            Description = "Sembrar un frijol",
            Type = "Assignment",
            Subject = "Ciencias Naturales",
            GradeLevel = "1°",
            CourseId = 1,
            MaxScore = 5.0m
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.True(await db.Tareas.AnyAsync(t => t.CursoId == 1 && t.Titulo == "Experimento de Plantas"));
    }

    [Fact]
    public async Task CreateContent_InCourseBelongingToAnotherTeacher_Returns403Forbidden_PreventingIDOR()
    {
        using var db = CreateSeededDb();
        // Docente A intenta publicar una tarea en el Curso 2 que pertenece al Docente B
        var controller = CreateController(db, "docente.a");

        var result = await controller.CreateContent(new CreateContentDto
        {
            Title = "Tarea Inyectada por IDOR",
            Description = "Intento de ataque BOLA",
            Type = "Assignment",
            Subject = "Matematicas",
            GradeLevel = "1°",
            CourseId = 2,
            MaxScore = 5.0m
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.False(await db.Tareas.AnyAsync(t => t.Titulo == "Tarea Inyectada por IDOR"));
    }

    [Fact]
    public async Task GradeSubmission_InAnotherTeachersCourse_Returns403Forbidden_PreventingIDOR()
    {
        using var db = CreateSeededDb();
        // Docente A intenta calificar la Entrega 50 que pertenece al Curso 2 (Docente B)
        var controller = CreateController(db, "docente.a");

        var result = await controller.GradeSubmission(50, new GradeSubmissionDto
        {
            Grade = 5.0m,
            Feedback = "Intento de alterar nota ajena"
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);

        var entrega = await db.Entregas.FindAsync(50);
        Assert.Null(entrega!.Calificacion);
    }

    [Fact]
    public async Task GradeSubmission_InOwnCourse_WithOutOfRangeScore_Returns400BadRequest()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "docente.b");

        var result = await controller.GradeSubmission(50, new GradeSubmissionDto
        {
            Grade = 15.0m, // Supera el puntaje máximo de 5.0
            Feedback = "Nota invalida"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GradeSubmission_InOwnCourse_WithValidScore_UpdatesGradeAndReturns200()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "docente.b");

        var result = await controller.GradeSubmission(50, new GradeSubmissionDto
        {
            Grade = 4.8m,
            Feedback = "Excelente trabajo"
        });

        Assert.IsType<OkObjectResult>(result);
        var entrega = await db.Entregas.FindAsync(50);
        Assert.Equal(4.8m, entrega!.Calificacion);
        Assert.Equal("CALIFICADA", entrega.Estado);
    }

    [Fact]
    public async Task EnrollStudent_InAnotherTeachersCourse_Returns403Forbidden()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "docente.a");

        var result = await controller.EnrollStudent(2, new EnrollStudentDto
        {
            StudentId = "alumno.uno"
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task UpdateActivity_OfAnotherTeachersCourse_Returns403Forbidden()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "docente.a");

        var result = await controller.UpdateActivity(10, "Tarea", new UpdateActivityDto
        {
            Title = "Titulo Saboteado",
            Description = "Modificacion no autorizada"
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task GetGradebook_OfAnotherTeachersCourse_Returns403Forbidden()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "docente.a");

        var result = await controller.GetGradebook(courseId: 2);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task GetAssignmentSubmissions_OfAnotherTeachersTask_Returns403Forbidden_PreventingIDOR()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "docente.a");

        var result = await controller.GetAssignmentSubmissions("10"); // Tarea 10 pertenece a Curso 2 (Docente B)

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task GetStudentSemaforoStatusById_OfStudentInUnassignedGradeAndCourse_Returns403Forbidden_PreventingIDOR()
    {
        using var db = CreateSeededDb();
        db.Usuarios.Add(new Usuario { Id = 400, RolId = 3, Username = "alumno.grado2", Nombre = "Laura", Apellido = "Rios", PasswordHash = "h", Activo = true });
        db.Alumnos.Add(new Alumno { UsuarioId = 400, GradoId = 2 });
        await db.SaveChangesAsync();

        var controller = CreateController(db, "docente.a"); // Docente A solo dicta en Grado 1

        var result = await controller.GetStudentSemaforoStatusById(400);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private sealed class NoOpFileStorageService : IFileStorageService
    {
        public string ProviderName => "NoOp";
        public Task<StoredFile> SaveFileAsync(IFormFile file, string scope, int ownerUserId, string ownerRole, CancellationToken cancellationToken = default)
            => Task.FromResult(new StoredFile { Success = true, Url = "/api/files/resources/test.pdf", StorageKey = "resources/test.pdf" });
        public Task<StoredFileMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<StoredFileMetadata?>(null);
        public Task<Stream?> OpenReadStreamAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(null);
        public Task<bool> DeleteAsync(string? urlOrStorageKey, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public string? ResolvePhysicalPath(string storageKey) => null;
        public string? ResolveLegacyPhysicalPath(string legacyPath) => null;
        public string GetUrl(string storageKey) => $"/api/files/{storageKey}";
    }
}
