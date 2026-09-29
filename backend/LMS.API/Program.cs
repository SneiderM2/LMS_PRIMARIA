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
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=db.rjvsbjjmlmvcihfgbiaz.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=Sneider0124;SSL Mode=Require;Trust Server Certificate=true;";

// Normalizar formato URI si el usuario configuró formato postgresql://...
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

        connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;";
    }
    catch
    {
        connectionString = normalizedUri;
    }
}

builder.Services.AddDbContext<LMSDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Inyección de Dependencias de Servicios de Dominio
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISemaforoService, SemaforoService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddHttpClient<ICaptchaService, GoogleRecaptchaService>();

// 3. Configuración de Autenticación JWT
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

// 4. Configuración de Políticas de Autorización
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin", "ADMINISTRADOR", ".admin", "admin"));
    options.AddPolicy("RequireTeacher", policy => policy.RequireRole("Teacher", "DOCENTE", "Admin", "ADMINISTRADOR", ".admin"));
    options.AddPolicy("RequireStudent", policy => policy.RequireRole("Student", "ALUMNO"));
});

// 5. Configuración de CORS para el Frontend Angular (incluye acceso móvil por red local)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Permite localhost, 127.0.0.1 y cualquier IP de red local (ej. 192.168.x.x)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 6. Swagger / OpenAPI con soporte JWT Bearer para .NET 8 LTS
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LMS Primaria - API REST",
        Version = "v1",
        Description = "Backend para la plataforma educativa LMS de Educación Primaria con Semáforo de Asistencia y Entregas de Tareas."
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

// 7. Migración e Inicialización de Semillas en la Base de Datos
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<LMSDbContext>();
        await DbInitializer.SeedAsync(context);
        app.Logger.LogInformation("Base de datos PostgreSQL (Supabase) inicializada con datos de prueba escolares exitosamente.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Ocurrió un error al inicializar la base de datos.");
    }
}

// 8. Configurar directorio de logs
var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logsPath);
FileLogger.SetLogDirectory(logsPath);
await FileLogger.InfoAsync("Servidor LMS iniciado", new { environment = app.Environment.EnvironmentName });

// 9. Pipeline HTTP — Middlewares personalizados (orden importa)
app.UseGlobalExceptionHandler(); // Primero: captura excepciones de todo el pipeline
app.UseSecurityHeaders();         // Segundo: inyecta cabeceras de seguridad
app.UseRequestLogging();          // Tercero: registra cada petición en access.log/error.log

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
app.UseStaticFiles(); // Habilitar descargas de archivos en wwwroot/uploads

app.UseRouting();
app.UseCors("AllowAngularDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
