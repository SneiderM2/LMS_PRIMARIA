using System.Text;
using LMS.API.Data;
using LMS.API.Middleware;
using LMS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de MySQL y Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Port=3306;Database=lms_scikids;Uid=root;Pwd=root;CharSet=utf8mb4;";

builder.Services.AddDbContext<LMSDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 2. Inyección de Dependencias de Servicios de Dominio
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISemaforoService, SemaforoService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

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

// 5. Configuración de CORS para el Frontend Angular
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 6. OpenAPI nativo .NET 10 con soporte JWT Bearer
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new()
        {
            Title = "LMS Primaria - API REST",
            Version = "v1",
            Description = "Backend para la plataforma educativa LMS de Educación Primaria con Semáforo de Asistencia y Entregas de Tareas."
        };
        return Task.CompletedTask;
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
        app.Logger.LogInformation("Base de datos MySQL inicializada con datos de prueba escolares exitosamente.");
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
    // Scalar UI: interfaz moderna para explorar la API (reemplaza Swagger UI)
    // Acceso en: http://localhost:5000/scalar/v1
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "LMS Primaria - API REST";
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
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
