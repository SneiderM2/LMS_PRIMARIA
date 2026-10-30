using System.Security.Claims;
using System.Text;
using LMS.API.Configuration;
using LMS.API.Controllers;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LMS.API.Tests;

public class StudentControllerSecurityTests
{
    private static LMSDbContext CreateSeededDb()
    {
        var options = new DbContextOptionsBuilder<LMSDbContext>()
            .UseInMemoryDatabase(databaseName: $"StudentTestDb_{Guid.NewGuid():N}")
            .Options;
        var db = new LMSDbContext(options);

        db.Roles.AddRange(
            new Rol { Id = 2, Nombre = "DOCENTE" },
            new Rol { Id = 3, Nombre = "ALUMNO" }
        );
        db.Grados.AddRange(
            new Grado { Id = 1, Nombre = "1°" },
            new Grado { Id = 5, Nombre = "5°" }
        );

        db.Usuarios.AddRange(
            new Usuario { Id = 10, RolId = 2, Username = "profe.ciencias", Nombre = "Mario", Apellido = "Vargas", PasswordHash = "h", Activo = true },
            new Usuario { Id = 50, RolId = 3, Username = "alumno.primero", Nombre = "Sofia", Apellido = "Castro", PasswordHash = "h", Activo = true, DataPolicyAccepted = true, DataPolicyVersion = "2026-10-19" }
        );
        db.Alumnos.Add(new Alumno { UsuarioId = 50, GradoId = 1 });
        db.ConsentimientosHabeasData.Add(new ConsentimientoHabeasData
        {
            Id = 1,
            AlumnoId = 50,
            UsuarioId = 50,
            AcudienteNombre = "Acudiente Sofia Castro",
            AcudienteDocumento = "****789",
            TipoDocumento = "CC",
            VersionPolitica = "2026-10-19",
            HashTextoPolitica = "sha256-policy-hash",
            AutorizaServicioEducativo = true,
            AutorizaDatosOpcionales = false,
            AutorizaTransmisionInternacional = true,
            IpOrigen = "127.0.0.1",
            UserAgent = "UnitTest",
            FechaAutorizacionUtc = DateTime.UtcNow,
            FechaRevocatoriaUtc = null
        });

        // Curso 1 es de 1° grado; Curso 5 es de 5° grado
        db.Cursos.AddRange(
            new Curso { Id = 1, Nombre = "Lectura 1°", GradoId = 1, Grupo = "A", DocenteId = 10, Activo = true },
            new Curso { Id = 5, Nombre = "Ciencias 5°", GradoId = 5, Grupo = "A", DocenteId = 10, Activo = true }
        );

        db.Tareas.AddRange(
            new Tarea { Id = 101, CursoId = 1, Titulo = "Cuento Infantil", FechaLimite = DateTime.UtcNow.AddDays(5), PuntajeMaximo = 5.0m, Activo = true },
            new Tarea { Id = 505, CursoId = 5, Titulo = "Ensayo Quinto Grado", FechaLimite = DateTime.UtcNow.AddDays(5), PuntajeMaximo = 5.0m, Activo = true }
        );

        db.SaveChanges();
        return db;
    }

    private static StudentController CreateController(LMSDbContext db, string username)
    {
        var storageOptions = Options.Create(new StorageOptions
        {
            MaxFileSizeBytes = 10 * 1024 * 1024,
            MaxFilesPerSubmission = 3
        });

        var controller = new StudentController(
            db,
            new StubStorageService(),
            storageOptions,
            NullLogger<StudentController>.Instance);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Role, "Student")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Fact]
    public async Task EnrollInCourse_OfSameGrade_SucceedsWith200Ok()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "alumno.primero");

        var result = await controller.EnrollInCourse(courseId: 1);

        Assert.IsType<OkObjectResult>(result);
        Assert.True(await db.Inscripciones.AnyAsync(i => i.AlumnoId == 50 && i.CursoId == 1 && i.Estado == "ACTIVA"));
    }

    [Fact]
    public async Task EnrollInCourse_OfDifferentGrade_Returns403Forbidden_PreventingCrossGradeBOLA()
    {
        using var db = CreateSeededDb();
        // Alumno de 1° intenta inscribirse forzadamente en Curso 5 (de 5° grado)
        var controller = CreateController(db, "alumno.primero");

        var result = await controller.EnrollInCourse(courseId: 5);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.False(await db.Inscripciones.AnyAsync(i => i.AlumnoId == 50 && i.CursoId == 5));
    }

    [Fact]
    public async Task UploadAssignment_ForTaskInUnenrolledOtherGradeCourse_Returns403Forbidden()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "alumno.primero");

        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 fake pdf content");
        var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "tarea.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await controller.UploadAssignment(new UploadAssignmentDto
        {
            AssignmentId = "505", // Pertenece a 5° grado
            File = formFile,
            StudentComment = "Intento de entrega en grado ajeno"
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.False(await db.Entregas.AnyAsync(e => e.TareaId == 505));
    }

    [Fact]
    public async Task UploadAssignment_ForTaskInOwnGradeCourse_SavesSubmissionAndReturns200Ok()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "alumno.primero");

        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 valid submission");
        var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "mi_cuento.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await controller.UploadAssignment(new UploadAssignmentDto
        {
            AssignmentId = "101",
            File = formFile,
            StudentComment = "Aquí está mi cuento"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<UploadResultDto>(ok.Value);
        Assert.True(dto.Success);
        Assert.True(await db.Entregas.AnyAsync(e => e.TareaId == 101 && e.AlumnoId == 50));
    }

    [Fact]
    public async Task WithdrawFromCourse_NotEnrolled_Returns404NotFound()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "alumno.primero");

        var result = await controller.WithdrawFromCourse(courseId: 1);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetStudentDashboard_WhenMinorConsentRevoked_Returns403Forbidden()
    {
        using var db = CreateSeededDb();
        var consent = await db.ConsentimientosHabeasData.FirstAsync(c => c.AlumnoId == 50);
        consent.FechaRevocatoriaUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var controller = CreateController(db, "alumno.primero");
        var result = await controller.GetStudentDashboard();

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private sealed class StubStorageService : IFileStorageService
    {
        public string ProviderName => "Stub";
        public Task<StoredFile> SaveFileAsync(IFormFile file, string scope, int ownerUserId, string ownerRole, CancellationToken cancellationToken = default)
            => Task.FromResult(new StoredFile
            {
                Success = true,
                Url = $"/api/files/{scope}/0123456789abcdef0123456789abcdef.pdf",
                StorageKey = $"{scope}/0123456789abcdef0123456789abcdef.pdf",
                OriginalName = file.FileName,
                ContentType = "application/pdf",
                Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                Size = file.Length
            });

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
