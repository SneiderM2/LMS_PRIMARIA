using System.Text;
using LMS.API.Data;
using LMS.API.Middleware;
using LMS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Compatibilidad universal de DateTime con PostgreSQL en Supabase
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de PostgreSQL (Supabase) y Entity Framework Core
var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Host=aws-0-us-east-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.rjvsbjjmlmvcihfgbiaz;Password=Sneider0124;SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=15;Connection Idle Lifetime=300;Keepalive=30;Tcp Keepalive=true;";

var connectionString = rawConnectionString;
if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
    connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
{
    var normalizedUri = connectionString.Replace("[Sneider0124]", "Sneider0124");
    try
    {
        var uri = new Uri(normalizedUri);
        var userInfo = uri.UserInfo.Split(':');
        var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var db = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrWhiteSpace(db)) db = "postgres";

        if (host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
        {
            var projectRef = "rjvsbjjmlmvcihfgbiaz";
            if (host.StartsWith("db.", StringComparison.OrdinalIgnoreCase))
            {
                var parts = host.Split('.');
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    projectRef = parts[1];
                }
            }

            host = "aws-0-us-east-1.pooler.supabase.com";
            port = 5432;
            if (!user.Contains('.'))
            {
                user = $"postgres.{projectRef}";
            }
        }
        else if (host.Contains("pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
        {
            port = 5432;
            if (!user.Contains('.'))
            {
                user = $"{user}.rjvsbjjmlmvcihfgbiaz";
            }
        }

        connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=15;Connection Idle Lifetime=300;Keepalive=30;Tcp Keepalive=true;";
    }
    catch
    {
        connectionString = normalizedUri;
    }
}
else
{
    if (connectionString.Contains("pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
    {
        connectionString = System.Text.RegularExpressions.Regex.Replace(connectionString, @"Port=\d+", "Port=5432", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}

builder.Services.AddDbContext<LMSDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(30);
        npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
    }));

// 2. Inyección de Dependencias
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISemaforoService, SemaforoService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddHttpClient<ICaptchaService, GoogleRecaptchaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(4);
});

// 3. Autenticación JWT
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "LmsPrimarySchoolSuperSecretKey2026!@#$%^&*()_+";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LMS.API";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "LMS.Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 4. Políticas de Autorización
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin", "ADMINISTRADOR", ".admin", "admin"));
    options.AddPolicy("RequireTeacher", policy => policy.RequireRole("Teacher", "DOCENTE", "Admin", "ADMINISTRADOR", ".admin"));
    options.AddPolicy("RequireStudent", policy => policy.RequireRole("Student", "ALUMNO"));
});

// 5. Configuración de CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins(
            "https://sneiderm2.github.io",
            "https://sneiderm2.github.io/LMS_PRIMARIA",
            "http://localhost:4200",
            "http://localhost:5000",
            "http://127.0.0.1:4200"
        )
        .SetIsOriginAllowed(origin => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 6. Swagger / OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LMS Primaria - API REST",
        Version = "v1",
        Description = "Backend para la plataforma educativa LMS de Educación Primaria."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT en el formato: Bearer {su_token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();


// 7. Logs (solo I/O local, no bloquea red)
var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
try { Directory.CreateDirectory(logsPath); } catch { logsPath = Path.Combine(Path.GetTempPath(), "lms-logs"); Directory.CreateDirectory(logsPath); }
FileLogger.SetLogDirectory(logsPath);

// 8. Inicialización de BD en Supabase — se ejecuta DESPUÉS de que Kestrel empieza a escuchar.
//    Esto evita que el health-check de Render falle por timeout durante el arranque.
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        // Dar 200 ms para que el servidor esté 100% operativo antes de tocar la BD
        await Task.Delay(200);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var scope = app.Services.CreateScope();
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<LMSDbContext>();
            await DbInitializer.SeedAsync(context);
            app.Logger.LogInformation("✅ Base de datos PostgreSQL (Supabase) inicializada correctamente (post-startup).");
        }
        catch (OperationCanceledException)
        {
            app.Logger.LogWarning("⚠️  DbInitializer cancelado por timeout (25 s). El servidor sigue operando con normalidad.");
        }
        catch (Exception ex)
        {
            // NO relanzar — un error de semilla no debe tumbar el servidor
            app.Logger.LogError(ex, "⚠️  Error no crítico en inicialización de BD (post-startup). El servidor continúa en línea.");
        }
    });
});


// 9. Middlewares
app.UseGlobalExceptionHandler();
app.UseSecurityHeaders();
app.UseRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LMS Primaria - API REST v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowAngularDev");

app.UseAuthentication();
app.UseSingleSessionValidation(); // Middleware de Sesión Única
app.UseAuthorization();

app.MapGet("/api/ping", () => Results.Ok(new 
{ 
    status = "healthy", 
    message = "LMS Primary School API is active and ready.",
    timestamp = DateTime.UtcNow 
}));

app.MapGet("/api/health", async (LMSDbContext context) =>
{
    try
    {
        var canConnect = await context.Database.CanConnectAsync();
        return Results.Ok(new 
        { 
            status = canConnect ? "healthy" : "degraded",
            database = canConnect ? "connected" : "error",
            timestamp = DateTime.UtcNow 
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "error", message = ex.Message }, statusCode: 503);
    }
});

app.MapControllers();

app.Run();