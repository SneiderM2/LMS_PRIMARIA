using System.Security.Claims;
using System.Text;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAdmin")]
public class AdminController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly IWebHostEnvironment _env;

    public AdminController(LMSDbContext context, ILogger<AdminController> logger, IWebHostEnvironment env)
    {
        _context = context;
        _logger = logger;
        _env = env;
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(AdminMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminMetrics()
    {
        var totalActive = await _context.Usuarios.CountAsync(u => u.Activo);
        var activeTeachers = await _context.Usuarios.CountAsync(u => u.Activo && u.Rol.Nombre == "DOCENTE");
        var activeStudents = await _context.Usuarios.CountAsync(u => u.Activo && u.Rol.Nombre == "ALUMNO");
        var totalCourses = await _context.Cursos.CountAsync(c => c.Activo);

        string systemStatus = "Operativo";
        try
        {
            if (!await _context.Database.CanConnectAsync()) systemStatus = "Degradado (Fallo BD)";
        }
        catch
        {
            systemStatus = "Degradado (Fallo BD)";
        }

        int recentErrors = 0;
        try
        {
            var logsDir = FileLogger.LogDirectory;
            if (!Directory.Exists(logsDir)) logsDir = Path.Combine(AppContext.BaseDirectory, "logs");

            var errorLogPath = Path.Combine(logsDir, "error.log");
            if (System.IO.File.Exists(errorLogPath))
            {
                var todayPrefix = DateTime.Now.ToString("yyyy-MM-dd");
                using var fs = new FileStream(errorLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("[" + todayPrefix) && (line.Contains("[HTTP-") || line.Contains("[EXCEPTION]")))
                    {
                        recentErrors++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al calcular métricas de error.log");
        }

        return Ok(new AdminMetricsDto
        {
            TotalActiveUsers = totalActive,
            ActiveTeachersCount = activeTeachers,
            ActiveStudentsCount = activeStudents,
            SystemStatus = systemStatus,
            RecentErrorsCount = recentErrors,
            TotalCoursesCount = totalCourses
        });
    }

    [HttpGet("backup-database")]
    [Produces("application/sql")]
    public async Task<IActionResult> BackupDatabase()
    {
        try
        {
            var sqlBuilder = new StringBuilder();
            var timestamp = DateTime.UtcNow;

            sqlBuilder.AppendLine("-- ====================================================================");
            sqlBuilder.AppendLine("-- LMS PRIMARIA - RESPALDO / BACKUP DE BASE DE DATOS");
            sqlBuilder.AppendLine($"-- FECHA DE EXTRACCIÓN (UTC): {timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sqlBuilder.AppendLine("-- MOTOR DE BASE DE DATOS: PostgreSQL (Supabase Cloud)");
            sqlBuilder.AppendLine("-- ====================================================================");
            sqlBuilder.AppendLine();
            sqlBuilder.AppendLine("SET statement_timeout = 0;");
            sqlBuilder.AppendLine("SET client_encoding = 'UTF8';");
            sqlBuilder.AppendLine("BEGIN;");
            sqlBuilder.AppendLine();

            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            var canonicalOrder = new List<string>
            {
                "roles", "grados", "usuarios", "alumnos", "cursos", "inscripciones", "tareas", "entregas", "archivos_entrega", "materiales"
            };

            var existingTables = new List<string>();
            using (var tablesCmd = conn.CreateCommand())
            {
                tablesCmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name;";
                using var reader = await tablesCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) existingTables.Add(reader.GetString(0));
            }

            var orderedTables = canonicalOrder.Where(t => existingTables.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
            foreach (var tbl in existingTables)
            {
                if (!orderedTables.Contains(tbl, StringComparer.OrdinalIgnoreCase)) orderedTables.Add(tbl);
            }

            foreach (var tableName in orderedTables)
            {
                sqlBuilder.AppendLine($"-- TABLA: \"{tableName}\"");
                var columns = new List<string>();
                using (var colCmd = conn.CreateCommand())
                {
                    colCmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @tbl ORDER BY ordinal_position;";
                    var p = colCmd.CreateParameter();
                    p.ParameterName = "@tbl";
                    p.Value = tableName;
                    colCmd.Parameters.Add(p);

                    using var colReader = await colCmd.ExecuteReaderAsync();
                    while (await colReader.ReadAsync()) columns.Add(colReader.GetString(0));
                }

                if (columns.Count == 0) continue;

                using (var dataCmd = conn.CreateCommand())
                {
                    dataCmd.CommandText = $"SELECT * FROM \"{tableName}\";";
                    using var dataReader = await dataCmd.ExecuteReaderAsync();
                    var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
                    var rowsDumped = 0;

                    while (await dataReader.ReadAsync())
                    {
                        var values = new List<string>();
                        for (int i = 0; i < dataReader.FieldCount; i++)
                        {
                            if (dataReader.IsDBNull(i)) values.Add("NULL");
                            else
                            {
                                var val = dataReader.GetValue(i);
                                switch (val)
                                {
                                    case bool b: values.Add(b ? "TRUE" : "FALSE"); break;
                                    case int or long or short or byte: values.Add(val.ToString()!); break;
                                    case decimal or double or float: values.Add(Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture)!); break;
                                    case DateTime dt: values.Add($"'{dt:yyyy-MM-dd HH:mm:ss.ffffff}'"); break;
                                    case DateTimeOffset dto: values.Add($"'{dto:yyyy-MM-dd HH:mm:ss.ffffffzzz}'"); break;
                                    case Guid g: values.Add($"'{g}'"); break;
                                    default: values.Add($"'{val.ToString()?.Replace("'", "''")}'"); break;
                                }
                            }
                        }
                        sqlBuilder.AppendLine($"INSERT INTO \"{tableName}\" ({columnList}) VALUES ({string.Join(", ", values)}) ON CONFLICT DO NOTHING;");
                        rowsDumped++;
                    }
                    sqlBuilder.AppendLine($"-- Registros exportados: {rowsDumped}");
                    sqlBuilder.AppendLine();
                }
            }

            sqlBuilder.AppendLine("COMMIT;");
            var sqlBytes = Encoding.UTF8.GetBytes(sqlBuilder.ToString());
            var fileName = $"backup_lms_supabase_{timestamp:yyyyMMdd_HHmmss}.sql";

            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");

            return File(sqlBytes, "application/sql", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando copia de seguridad SQL.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error al generar copia de seguridad SQL.", detail = ex.Message });
        }
    }

    [HttpPost("restore-backup")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RestoreBackup(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Debes seleccionar un archivo de copia de seguridad (.sql) válido." });
        }

        if (!file.FileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Formato no permitido. El archivo debe tener extensión .sql" });
        }

        try
        {
            string sqlContent;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                sqlContent = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(sqlContent))
            {
                return BadRequest(new { message = "El archivo SQL proporcionado está vacío." });
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 300;
            cmd.CommandText = sqlContent;

            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                message = "La base de datos se ha restaurado exitosamente desde el archivo SQL subido.",
                fileName = file.FileName,
                restoredAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar la restauración del archivo SQL.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Ocurrió un error al ejecutar el script de restauración en PostgreSQL.",
                detail = ex.Message
            });
        }
    }
}