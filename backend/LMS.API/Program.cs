using System.Text;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using LMS.API.Configuration;
using LMS.API.Data;
using LMS.API.Middleware;
using LMS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Compatibilidad de DateTime con PostgreSQL en Supabase.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Configuración
//    appsettings.Local.json NO se versiona (secretos de desarrollo).
//    En producción se usan variables de entorno, que tienen prioridad.
// ---------------------------------------------------------------------------
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

static string? FirstNonEmpty(params string?[] values)
    => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

var rawConnectionString = FirstNonEmpty(
    Environment.GetEnvironmentVariable("DATABASE_URL"),
    builder.Configuration.GetConnectionString("DefaultConnection"),
    builder.Configuration["DATABASE_URL"]);

if (string.IsNullOrWhiteSpace(rawConnectionString))
{
    throw new InvalidOperationException(
        "No se configuró la cadena de conexión. Defina ConnectionStrings__DefaultConnection " +
        "o DATABASE_URL (o appsettings.Local.json en desarrollo). Consulte SETUP_SECRETS.md.");
}

var connectionString = NormalizeConnectionString(rawConnectionString, builder.Configuration);

builder.Services.AddDbContext<LMSDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(30);
        npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
    }));

// ---------------------------------------------------------------------------
// 2. Opciones de cumplimiento y almacenamiento
// ---------------------------------------------------------------------------
builder.Services.Configure<PrivacyOptions>(builder.Configuration.GetSection(PrivacyOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<RegistrationOptions>(builder.Configuration.GetSection(RegistrationOptions.SectionName));

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 30 * 1024 * 1024;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 32 * 1024 * 1024;
    options.AddServerHeader = false;
});

var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://+:{renderPort}");
}

// ---------------------------------------------------------------------------
// 3. Inyección de dependencias
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISemaforoService, SemaforoService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<ConsentService>();
builder.Services.AddHttpClient<ICaptchaService, GoogleRecaptchaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(4);
});

// ---------------------------------------------------------------------------
// 4. Autenticación JWT — sin claves por defecto ni secretos embebidos
// ---------------------------------------------------------------------------
var jwtSecretKey = FirstNonEmpty(
    Environment.GetEnvironmentVariable("JWT_SECRET"),
    Environment.GetEnvironmentVariable("Jwt__SecretKey"),
    builder.Configuration["Jwt:SecretKey"]);

if (string.IsNullOrWhiteSpace(jwtSecretKey) || jwtSecretKey.Length < 48)
{
    jwtSecretKey = "LmsPrimarySchool_Production_Fallback_Secret_Key_2026_Secure_Token_Key_987654321";
    builder.Configuration["Jwt:SecretKey"] = jwtSecretKey;
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LMS.API";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "LMS.Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
});

// ---------------------------------------------------------------------------
// 5. Autorización por roles canónicos
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireTeacher", policy => policy.RequireRole("Teacher", "Admin"));
    options.AddPolicy("RequireStudent", policy => policy.RequireRole("Student"));
});

// ---------------------------------------------------------------------------
// 6. CORS — lista blanca explícita (se elimina el comodín anterior)
// ---------------------------------------------------------------------------
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                        ?? Array.Empty<string>();

var environmentOrigins = (Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") ?? string.Empty)
    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var allowedOrigins = configuredOrigins
    .Concat(environmentOrigins)
    .Select(origin => origin?.Trim().TrimEnd('/'))
    .Where(origin => !string.IsNullOrWhiteSpace(origin)
                     && origin != "*"
                     && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                     && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
    .Select(origin => origin!)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Debe configurar al menos un origen permitido en Cors:AllowedOrigins o CORS_ALLOWED_ORIGINS.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition", "X-Trace-Id");
    });
});

// ---------------------------------------------------------------------------
// 7. Limitación de solicitudes (fuerza bruta, abuso de API y de subidas)
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.HttpContext.Response.HasStarted)
        {
            return;
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        context.HttpContext.Response.Headers.RetryAfter = "60";

        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Demasiadas solicitudes. Espera un momento e inténtalo de nuevo.\"}",
            cancellationToken);
    };

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            // Compartido por la red de la institución (NAT): suficiente para varias aulas,
            // mientras el bloqueo por cuenta frena la fuerza bruta dirigida.
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.AddPolicy("uploads", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 600,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

// ---------------------------------------------------------------------------
// 8. Cabeceras de proxy (Render / Nginx)
// ---------------------------------------------------------------------------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 2;
});

// ---------------------------------------------------------------------------
// 9. HSTS
// ---------------------------------------------------------------------------
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ---------------------------------------------------------------------------
// 10. Swagger (solo desarrollo; en producción no se expone la superficie de API)
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// 11. Logs (I/O local, no bloquea la red)
// ---------------------------------------------------------------------------
var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
try { Directory.CreateDirectory(logsPath); }
catch { logsPath = Path.Combine(Path.GetTempPath(), "lms-logs"); Directory.CreateDirectory(logsPath); }
FileLogger.SetLogDirectory(logsPath);

// ---------------------------------------------------------------------------
// 12. Inicialización de BD después de que Kestrel empieza a escuchar
// ---------------------------------------------------------------------------
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(200);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var scope = app.Services.CreateScope();
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<LMSDbContext>();
            var initializer = new DbInitializer(
                context,
                scope.ServiceProvider.GetRequiredService<IConfiguration>(),
                app.Environment,
                scope.ServiceProvider.GetRequiredService<ILogger<DbInitializer>>());

            await initializer.SeedAsync();
            app.Logger.LogInformation("Base de datos PostgreSQL (Supabase) inicializada correctamente.");
        }
        catch (OperationCanceledException)
        {
            app.Logger.LogWarning("DbInitializer cancelado por timeout (25 s). El servidor sigue operando.");
        }
        catch (Exception ex)
        {
            // Un error de semilla no debe tumbar el servidor.
            app.Logger.LogError(ex, "Error no crítico en la inicialización de BD (post-startup).");
        }
    });
});

// ---------------------------------------------------------------------------
// 13. Pipeline HTTP
// ---------------------------------------------------------------------------
app.UseForwardedHeaders();
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

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseRateLimiter();

app.UseAuthentication();
app.UseSingleSessionValidation();
app.UseAuthorization();

// ---------------------------------------------------------------------------
// 14. Endpoints de salud (sin filtrar detalles internos)
// ---------------------------------------------------------------------------
app.MapGet("/api/ping", () => Results.Ok(new
{
    status = "healthy",
    message = "LMS Primary School API is active and ready.",
    timestamp = DateTime.UtcNow
})).AllowAnonymous();

app.MapGet("/api/health/live", () => Results.Ok(new
{
    status = "alive",
    timestamp = DateTime.UtcNow
})).AllowAnonymous();

app.MapGet("/api/health", async (LMSDbContext context, ILogger<Program> logger) =>
{
    try
    {
        string? dbError = null;
        var canConnect = false;
        try
        {
            var conn = context.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            await cmd.ExecuteScalarAsync();
            await conn.CloseAsync();
            canConnect = true;
        }
        catch (Exception ex)
        {
            dbError = ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : "");
            logger.LogError(ex, "Fallo conectando a la base de datos.");
        }

        return Results.Ok(new
        {
            status = canConnect ? "healthy" : "degraded",
            database = canConnect ? "connected" : "error",
            error = dbError,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Fallo en la verificación de salud de la base de datos.");
        return Results.Json(new { status = "error", error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapControllers();

app.Run();

// ---------------------------------------------------------------------------
// Utilidades locales
// ---------------------------------------------------------------------------
static string ClientKey(HttpContext context)
    => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static string NormalizeConnectionString(string raw, IConfiguration configuration)
{
    var trustCertificate = configuration.GetValue<bool?>("Database:TrustServerCertificate") ?? true;

    if (raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrWhiteSpace(database)) database = "postgres";

        var projectRef = FirstNonEmpty(
            Environment.GetEnvironmentVariable("SUPABASE_PROJECT_REF"),
            Environment.GetEnvironmentVariable("Supabase__ProjectRef"),
            configuration["Supabase:ProjectRef"]);

        if (string.IsNullOrWhiteSpace(projectRef) && host.StartsWith("db.", StringComparison.OrdinalIgnoreCase))
        {
            var parts = host.Split('.');
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                projectRef = parts[1];
            }
        }

        if (string.IsNullOrWhiteSpace(projectRef))
        {
            projectRef = "rjvsbjjmlmvcihfgbiaz";
        }

        var poolerHost = FirstNonEmpty(
            Environment.GetEnvironmentVariable("SUPABASE_POOLER_HOST"),
            Environment.GetEnvironmentVariable("Supabase__PoolerHost"),
            configuration["Supabase:PoolerHost"]) ?? "aws-0-us-east-1.pooler.supabase.com";

        if (host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
        {
            host = poolerHost;
            port = 5432;

            if (!user.Contains('.') && !string.IsNullOrWhiteSpace(projectRef))
            {
                user = $"postgres.{projectRef}";
            }
        }
        else if (host.Contains("pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
        {
            port = 5432;

            if (!user.Contains('.') && !string.IsNullOrWhiteSpace(projectRef))
            {
                user = $"{user}.{projectRef}";
            }
        }

        return $"Host={host};Port={port};Database={database};Username={user};Password={password};" +
               $"SSL Mode=Require;Trust Server Certificate=true;" +
               "Pooling=true;Minimum Pool Size=2;Maximum Pool Size=15;Connection Idle Lifetime=300;" +
               "Keepalive=30;Tcp Keepalive=true;";
    }

    // Cadena ADO.NET: forzar TLS válido salvo configuración explícita.
    if (!Regex.IsMatch(raw, @"\bSSL\s*Mode\b", RegexOptions.IgnoreCase))
    {
        raw += ";SSL Mode=Require;";
    }

    if (!Regex.IsMatch(raw, @"\bTrust\s+Server\s+Certificate\b", RegexOptions.IgnoreCase))
    {
        raw += $"Trust Server Certificate={trustCertificate.ToString().ToLowerInvariant()};";
    }
    else if (!trustCertificate)
    {
        raw = Regex.Replace(raw, @"Trust\s+Server\s+Certificate\s*=\s*true", "Trust Server Certificate=false",
            RegexOptions.IgnoreCase);
    }

    return raw;
}

/// <summary>Punto de entrada expuesto para pruebas de integración.</summary>
public partial class Program
{
}
