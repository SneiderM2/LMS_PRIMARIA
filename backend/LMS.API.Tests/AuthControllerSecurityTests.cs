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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LMS.API.Tests;

public class AuthControllerSecurityTests
{
    private const string TestJwtSecret = "test-only-jwt-secret-key-with-more-than-48-chars-2026-secure!";
    private const string TestEvidenceKey = "test-only-evidence-hmac-key-with-at-least-32-chars!";

    private static LMSDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LMSDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuthTestDb_{Guid.NewGuid():N}")
            .Options;
        var context = new LMSDbContext(options);

        context.Roles.AddRange(
            new Rol { Id = 1, Nombre = "ADMINISTRADOR", Descripcion = "Admin" },
            new Rol { Id = 2, Nombre = "DOCENTE", Descripcion = "Teacher" },
            new Rol { Id = 3, Nombre = "ALUMNO", Descripcion = "Student" }
        );
        context.Grados.AddRange(
            new Grado { Id = 1, Nombre = "1°", Descripcion = "Primero" },
            new Grado { Id = 2, Nombre = "2°", Descripcion = "Segundo" }
        );
        context.SaveChanges();
        return context;
    }

    private static AuthController CreateController(
        LMSDbContext db,
        bool captchaResult = true,
        ClaimsPrincipal? userPrincipal = null)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = TestJwtSecret,
            ["Jwt:Issuer"] = "LMS.API",
            ["Jwt:Audience"] = "LMS.Client",
            ["Jwt:ExpirationMinutes"] = "60"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
        var tokenService = new TokenService(config);

        var privacyOptions = Options.Create(new PrivacyOptions
        {
            PolicyVersion = "2026-10-19",
            RequireGuardianConsentForMinors = true,
            EvidenceHmacKey = TestEvidenceKey
        });

        var registrationOptions = Options.Create(new RegistrationOptions
        {
            AllowPublicStudentRegistration = true,
            TeacherInviteCode = "DOCENTE-SECRETO-2026"
        });

        var consentService = new ConsentService(privacyOptions);
        var captchaService = new StubCaptchaService(captchaResult);
        var hostEnv = new StubHostEnvironment("Production");

        var controller = new AuthController(
            db,
            tokenService,
            captchaService,
            consentService,
            config,
            privacyOptions,
            registrationOptions,
            hostEnv,
            NullLogger<AuthController>.Instance);

        var httpContext = new DefaultHttpContext();
        if (userPrincipal != null)
        {
            httpContext.User = userPrincipal;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkAndGeneratesSessionToken()
    {
        using var db = CreateInMemoryDb();
        var password = "Segura!2026#Login";
        db.Usuarios.Add(new Usuario
        {
            Id = 10,
            RolId = 2,
            Username = "profe.juan",
            Nombre = "Juan",
            Apellido = "Perez",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
            Activo = true,
            DataPolicyAccepted = true,
            DataPolicyVersion = "2026-10-19"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.Login(new LoginRequestDto
        {
            Id = "profe.juan",
            Password = password,
            CaptchaToken = "valid-captcha"
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponseDto>(okResult.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("Teacher", response.User.Role);

        var updatedUser = await db.Usuarios.FindAsync(10);
        Assert.NotNull(updatedUser!.SessionToken);
        Assert.Equal(0, updatedUser.AccessFailedCount);
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksAccountFor15MinutesWith423()
    {
        using var db = CreateInMemoryDb();
        var realPassword = "RealPassword!2026#";
        db.Usuarios.Add(new Usuario
        {
            Id = 20,
            RolId = 3,
            Username = "alumno.test",
            Nombre = "Carlos",
            Apellido = "Ruiz",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(realPassword, workFactor: 4),
            Activo = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Primeros 4 intentos fallidos -> 401 Unauthorized
        for (int i = 1; i <= 4; i++)
        {
            var attempt = await controller.Login(new LoginRequestDto
            {
                Id = "alumno.test",
                Password = "WrongPassword!123",
                CaptchaToken = "ok"
            });
            Assert.IsType<UnauthorizedObjectResult>(attempt);
        }

        // 5to intento fallido -> 423 Locked y cuenta bloqueada por 15 minutos
        var fifthAttempt = await controller.Login(new LoginRequestDto
        {
            Id = "alumno.test",
            Password = "WrongPassword!123",
            CaptchaToken = "ok"
        });
        var lockedResult = Assert.IsType<ObjectResult>(fifthAttempt);
        Assert.Equal(StatusCodes.Status423Locked, lockedResult.StatusCode);

        // Incluso enviando la contraseña correcta durante el bloqueo debe retornar 423 Locked
        var attemptWhileLocked = await controller.Login(new LoginRequestDto
        {
            Id = "alumno.test",
            Password = realPassword,
            CaptchaToken = "ok"
        });
        var stillLocked = Assert.IsType<ObjectResult>(attemptWhileLocked);
        Assert.Equal(StatusCodes.Status423Locked, stillLocked.StatusCode);
    }

    [Fact]
    public async Task Login_WithHoneypotOrInvalidCaptcha_RejectsRequest()
    {
        using var db = CreateInMemoryDb();
        var controllerWithHoneypot = CreateController(db, captchaResult: true);

        var honeypotRes = await controllerWithHoneypot.Login(new LoginRequestDto
        {
            Id = "admin",
            Password = "Password!2026#",
            HoneypotTrap = "bot-filled-this"
        });
        Assert.IsType<UnauthorizedObjectResult>(honeypotRes);

        var controllerFailedCaptcha = CreateController(db, captchaResult: false);
        var captchaRes = await controllerFailedCaptcha.Login(new LoginRequestDto
        {
            Id = "admin",
            Password = "Password!2026#",
            CaptchaToken = "forged-token"
        });
        Assert.IsType<BadRequestObjectResult>(captchaRes);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("ADMINISTRADOR")]
    [InlineData(".admin")]
    public async Task Register_PublicAttemptToCreateAdmin_Returns400BadRequest(string requestedRole)
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Register(new RegisterRequestDto
        {
            Id = "hacker.admin",
            FullName = "Intento Escalacion",
            Password = "Strong!Password2026#",
            Role = requestedRole,
            AcceptDataPolicy = true,
            DataPolicyVersion = "2026-10-19",
            CaptchaToken = "ok"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(await db.Usuarios.AnyAsync(u => u.Username == "hacker.admin"));
    }

    [Fact]
    public async Task Register_TeacherWithoutValidInviteCode_Returns400BadRequest()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Register(new RegisterRequestDto
        {
            Id = "docente.falso",
            FullName = "Docente Sin Codigo",
            Password = "Strong!Password2026#",
            Role = "Teacher",
            TeacherInviteCode = "CODIGO-INCORRECTO",
            AcceptDataPolicy = true,
            DataPolicyVersion = "2026-10-19",
            CaptchaToken = "ok"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_StudentWithoutGuardianConsent_Returns400BadRequest()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Register(new RegisterRequestDto
        {
            Id = "menor.sin.acudiente",
            FullName = "Menor Edad",
            Password = "Strong!Password2026#",
            Role = "Student",
            Grade = "1°",
            AcceptDataPolicy = true,
            DataPolicyVersion = "2026-10-19",
            GuardianAuthorization = false,
            CaptchaToken = "ok"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ChangePassword_RotatesSessionToken_InvalidatingPreviousCompromisedSessions()
    {
        using var db = CreateInMemoryDb();
        var oldPassword = "OldPassword!2026#";
        var newPassword = "NewPassword!2026$";
        var initialSessionToken = "old-session-token-12345";

        db.Usuarios.Add(new Usuario
        {
            Id = 30,
            RolId = 2,
            Username = "docente.seguro",
            Nombre = "Maria",
            Apellido = "Gomez",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(oldPassword, workFactor: 4),
            Activo = true,
            SessionToken = initialSessionToken
        });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "docente.seguro"),
            new Claim(ClaimTypes.Role, "Teacher")
        }, "TestAuth"));

        var controller = CreateController(db, userPrincipal: principal);

        var result = await controller.ChangePassword(new ChangePasswordDto
        {
            CurrentPassword = oldPassword,
            NewPassword = newPassword,
            ConfirmNewPassword = newPassword
        });

        Assert.IsType<OkObjectResult>(result);

        var updatedUser = await db.Usuarios.FindAsync(30);
        Assert.NotNull(updatedUser!.SessionToken);
        Assert.NotEqual(initialSessionToken, updatedUser.SessionToken);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, updatedUser.PasswordHash));
    }

    [Fact]
    public async Task Logout_NullifiesSessionTokenInDatabase()
    {
        using var db = CreateInMemoryDb();
        db.Usuarios.Add(new Usuario
        {
            Id = 40,
            RolId = 3,
            Username = "alumno.logout",
            Nombre = "Luis",
            Apellido = "Diaz",
            PasswordHash = "hash",
            Activo = true,
            SessionToken = "active-session-token"
        });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "alumno.logout")
        }, "TestAuth"));

        var controller = CreateController(db, userPrincipal: principal);
        var res = await controller.Logout();

        Assert.IsType<OkObjectResult>(res);
        var userInDb = await db.Usuarios.FindAsync(40);
        Assert.Null(userInDb!.SessionToken);
    }

    private sealed class StubCaptchaService : ICaptchaService
    {
        private readonly bool _result;
        public StubCaptchaService(bool result) => _result = result;
        public Task<bool> VerifyTokenAsync(string? token, string? remoteIp = null) => Task.FromResult(_result);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public StubHostEnvironment(string envName) => EnvironmentName = envName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "LMS.API.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
