using System.Text;
using LMS.API.Data;
using LMS.API.Middleware;
using LMS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de PostgreSQL (Supabase) y Entity Framework Core
var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Host=aws-0-us-east-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.rjvsbjjmlmvcihfgbiaz;Password=Sneider0124;SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Maximum Pool Size=10;Connection Idle Lifetime=60;";

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

        connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Maximum Pool Size=10;Connection Idle Lifetime=60;";
    }
    catch
    {
        connectionString = normalizedUri;
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
builder.Services.AddHttpClient<ICaptchaService, GoogleRecaptchaService>();

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

// 7. Migración e Inicialización en Supabase
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<LMSDbContext>();
        await DbInitializer.SeedAsync(context);
        app.Logger.LogInformation("Base de datos PostgreSQL (Supabase) inicializada correctamente.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Ocurrió un error al inicializar la base de datos.");
    }
}

// 8. Logs
var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logsPath);
FileLogger.SetLogDirectory(logsPath);
await FileLogger.InfoAsync("Servidor LMS iniciado", new { environment = app.Environment.EnvironmentName });

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

app.MapControllers();

app.Run();