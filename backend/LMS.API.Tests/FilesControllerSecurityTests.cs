using System.Security.Claims;
using System.Text;
using LMS.API.Controllers;
using LMS.API.Data;
using LMS.API.Entities;
using LMS.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.API.Tests;

public class FilesControllerSecurityTests
{
    private const string ValidSubmissionPath = "submissions/aabbccddeeff00112233445566778899.pdf";

    private static LMSDbContext CreateSeededDb()
    {
        var options = new DbContextOptionsBuilder<LMSDbContext>()
            .UseInMemoryDatabase(databaseName: $"FilesTestDb_{Guid.NewGuid():N}")
            .Options;
        var db = new LMSDbContext(options);

        db.Roles.AddRange(
            new Rol { Id = 1, Nombre = "ADMINISTRADOR" },
            new Rol { Id = 2, Nombre = "DOCENTE" },
            new Rol { Id = 3, Nombre = "ALUMNO" }
        );
        db.Grados.Add(new Grado { Id = 1, Nombre = "1°" });

        // Docente propietario del curso (ID=10), Docente ajeno (ID=11), Alumno dueño (ID=20), Alumno intruso (ID=21)
        db.Usuarios.AddRange(
            new Usuario { Id = 10, RolId = 2, Username = "docente.titular", Nombre = "Carlos", Apellido = "Solis", PasswordHash = new string('h', 4), Activo = true }, // ggignore
            new Usuario { Id = 11, RolId = 2, Username = "docente.ajeno", Nombre = "Pedro", Apellido = "Rojas", PasswordHash = new string('h', 4), Activo = true }, // ggignore
            new Usuario { Id = 20, RolId = 3, Username = "alumno.dueno", Nombre = "Lucia", Apellido = "Paz", PasswordHash = new string('h', 4), Activo = true }, // ggignore
            new Usuario { Id = 21, RolId = 3, Username = "alumno.intruso", Nombre = "Mateo", Apellido = "Gil", PasswordHash = new string('h', 4), Activo = true } // ggignore
        );
        db.Alumnos.AddRange(
            new Alumno { UsuarioId = 20, GradoId = 1 },
            new Alumno { UsuarioId = 21, GradoId = 1 }
        );

        db.Cursos.Add(new Curso { Id = 1, Nombre = "Historia", GradoId = 1, Grupo = "A", DocenteId = 10, Activo = true });
        db.Tareas.Add(new Tarea { Id = 1, CursoId = 1, Titulo = "Mapa", FechaLimite = DateTime.UtcNow.AddDays(2), PuntajeMaximo = 5.0m, Activo = true });
        db.Entregas.Add(new Entrega { Id = 1, TareaId = 1, AlumnoId = 20, Estado = "ENVIADA", FechaEntrega = DateTime.UtcNow });
        db.ArchivosEntrega.Add(new ArchivoEntrega
        {
            Id = 1,
            EntregaId = 1,
            NombreOriginal = "mapa_lucia.pdf",
            NombreArchivo = "aabbccddeeff00112233445566778899.pdf",
            RutaArchivo = "/api/files/" + ValidSubmissionPath,
            TipoMime = "application/pdf",
            TamanoBytes = 128
        });

        db.SaveChanges();
        return db;
    }

    private static FilesController CreateController(LMSDbContext db, string username, string role)
    {
        var controller = new FilesController(
            db,
            new InMemoryStreamStorageService(),
            NullLogger<FilesController>.Instance);

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

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("..\\..\\appsettings.Local.json")]
    [InlineData("submissions/..%2f..%2fProgram.cs")]
    [InlineData("submissions/archivo.pdf%00.exe")]
    public async Task Download_WithPathTraversalOrNullByte_Returns400BadRequest(string maliciousPath)
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "alumno.dueno", "Student");

        var result = await controller.Download(maliciousPath);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Download_SubmissionOfAnotherStudent_Returns403Forbidden_PreventingIDOR()
    {
        using var db = CreateSeededDb();
        // Alumno intruso intenta descargar el archivo entregado por Lucia (alumno.dueno)
        var controller = CreateController(db, "alumno.intruso", "Student");

        var result = await controller.Download(ValidSubmissionPath);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Download_SubmissionByUnrelatedTeacher_Returns403Forbidden_PreventingIDOR()
    {
        using var db = CreateSeededDb();
        // Docente ajeno al curso intenta descargar el archivo de un estudiante que no es de su curso
        var controller = CreateController(db, "docente.ajeno", "Teacher");

        var result = await controller.Download(ValidSubmissionPath);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Download_OwnSubmission_ReturnsFileStreamWithSafeHeaders()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "alumno.dueno", "Student");

        var result = await controller.Download(ValidSubmissionPath);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Fact]
    public async Task Download_SubmissionByCourseTeacher_ReturnsFileStreamResult()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "docente.titular", "Teacher");

        var result = await controller.Download(ValidSubmissionPath);

        Assert.IsType<FileStreamResult>(result);
    }

    private sealed class InMemoryStreamStorageService : IFileStorageService
    {
        public string ProviderName => "Memory";
        public Task<StoredFile> SaveFileAsync(IFormFile file, string scope, int ownerUserId, string ownerRole, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<StoredFileMetadata?> GetMetadataAsync(string storagePath, CancellationToken cancellationToken = default)
            => Task.FromResult<StoredFileMetadata?>(new StoredFileMetadata
            {
                Scope = "submissions",
                OwnerUserId = 20,
                OwnerRole = "ALUMNO",
                OriginalName = "mapa_lucia.pdf",
                ContentType = "application/pdf",
                Size = 16
            });

        public Task<Stream?> OpenReadStreamAsync(string storagePath, CancellationToken cancellationToken = default)
        {
            if (storagePath == ValidSubmissionPath)
            {
                Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 content"));
                return Task.FromResult<Stream?>(stream);
            }
            return Task.FromResult<Stream?>(null);
        }

        public Task<bool> DeleteAsync(string? urlOrStorageKey, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public string? ResolvePhysicalPath(string storagePath) => null;
        public string? ResolveLegacyPhysicalPath(string legacyPath) => null;
        public string GetUrl(string storagePath) => $"/api/files/{storagePath}";
    }
}
