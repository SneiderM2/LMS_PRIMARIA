using System.Security.Claims;
using System.Text.Json;
using LMS.API.Configuration;
using LMS.API.Data;
using LMS.API.Entities;
using LMS.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LMS.API.Tests;

public class MiddlewareSecurityTests
{
    [Fact]
    public async Task SecurityHeadersMiddleware_AppliesStrictHeadersAndNoStoreCacheOnSensitiveRoutes()
    {
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/student/dashboard";

        await middleware.InvokeAsync(context);

        Assert.Contains("default-src 'none'", context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.Contains("frame-ancestors 'none'", context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Contains("max-age=31536000", context.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Contains("no-store", context.Response.Headers["Cache-Control"].ToString());
    }

    [Fact]
    public async Task SingleSessionMiddleware_WhenSessionTokenDiffers_Returns401DuplicateSession()
    {
        var dbName = $"SessionMiddlewareDb_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<LMSDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LMSDbContext>();
            db.Roles.Add(new Rol { Id = 3, Nombre = "ALUMNO" });
            db.Usuarios.Add(new Usuario
            {
                Id = 1,
                RolId = 3,
                Username = "alumno.sesion",
                Nombre = "Juan",
                Apellido = "Diaz",
                PasswordHash = "h",
                Activo = true,
                SessionToken = "nuevo-token-otro-dispositivo",
                DataPolicyAccepted = true,
                DataPolicyVersion = "2026-10-19"
            });
            await db.SaveChangesAsync();
        }

        var nextCalled = false;
        var middleware = new SingleSessionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<SingleSessionMiddleware>.Instance,
            Options.Create(new PrivacyOptions { PolicyVersion = "2026-10-19" }));

        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Request.Path = "/api/student/dashboard";
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "alumno.sesion"),
            new Claim("session_token", "token-antiguo-revocado")
        }, "Bearer"));

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("DUPLICATE_SESSION", body);
    }

    [Fact]
    public async Task SingleSessionMiddleware_WhenPolicyNotAccepted_Returns403PolicyAcceptanceRequired()
    {
        var dbName = $"PolicyMiddlewareDb_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<LMSDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LMSDbContext>();
            db.Roles.Add(new Rol { Id = 3, Nombre = "ALUMNO" });
            db.Usuarios.Add(new Usuario
            {
                Id = 2,
                RolId = 3,
                Username = "alumno.sin.politica",
                Nombre = "Sara",
                Apellido = "Mora",
                PasswordHash = "h",
                Activo = true,
                SessionToken = "sesion-valida",
                DataPolicyAccepted = false,
                DataPolicyVersion = null
            });
            await db.SaveChangesAsync();
        }

        var nextCalled = false;
        var middleware = new SingleSessionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<SingleSessionMiddleware>.Instance,
            Options.Create(new PrivacyOptions { PolicyVersion = "2026-10-19" }));

        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Request.Path = "/api/student/dashboard";
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "alumno.sin.politica"),
            new Claim("session_token", "sesion-valida")
        }, "Bearer"));

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("POLICY_ACCEPTANCE_REQUIRED", body);
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_InProduction_HidesSensitiveExceptionMessageAndStackTrace()
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new Exception("FATAL_DB_SECRET: Host=internal.db;Password=SuperSecretPassword123"),
            NullLogger<GlobalExceptionMiddleware>.Instance,
            new StubEnv("Production"));

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/student/courses";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("application/problem+json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        // Nunca debe filtrar contraseñas, cadenas de conexión ni stack traces en producción
        Assert.DoesNotContain("FATAL_DB_SECRET", body);
        Assert.DoesNotContain("SuperSecretPassword123", body);
    }

    private sealed class StubEnv : IHostEnvironment
    {
        public StubEnv(string envName) => EnvironmentName = envName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "LMS.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
