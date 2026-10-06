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

    /// <summary>
    /// Consulta el directorio de usuarios (Alumnos, Docentes y Administradores).
    /// GET: /api/admin/users?role=&search=
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(List<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] string? role = null, [FromQuery] string? search = null)
    {
        var query = _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(role) && !role.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var roleUpper = role.ToUpperInvariant();
            if (roleUpper == "STUDENT" || roleUpper == "ALUMNO")
                query = query.Where(u => u.Rol.Nombre == "ALUMNO");
            else if (roleUpper == "TEACHER" || roleUpper == "DOCENTE")
                query = query.Where(u => u.Rol.Nombre == "DOCENTE");
            else if (roleUpper.Contains("ADMIN"))
                query = query.Where(u => u.Rol.Nombre == "ADMINISTRADOR" || u.Rol.Nombre == ".admin");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(s) ||
                                     u.Nombre.ToLower().Contains(s) ||
                                     u.Apellido.ToLower().Contains(s));
        }

        var users = await query
            .OrderByDescending(u => u.FechaCreacion)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.Username,
                Nombre = u.Nombre,
                Apellido = u.Apellido,
                Role = u.Rol.Nombre == "ALUMNO" ? "Student" : (u.Rol.Nombre == "DOCENTE" ? "Teacher" : "Admin"),
                GradeLevel = (u.Alumno != null && u.Alumno.Grado != null) ? u.Alumno.Grado.Nombre : null,
                Activo = u.Activo,
                AvatarUrl = u.AvatarUrl,
                FechaCreacion = u.FechaCreacion
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Registra una nueva cuenta de usuario (Docente, Estudiante o Administrador).
    /// POST: /api/admin/users
    /// </summary>
    [HttpPost("users")]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateAdminUserDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cleanUsername = dto.Username.Trim();
        if (await _context.Usuarios.AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower()))
        {
            return BadRequest(new { message = "El carnet / documento ya existe en el sistema." });
        }

        var targetRoleName = "ALUMNO";
        var r = dto.Role.ToUpperInvariant();
        if (r.Contains("DOCENTE") || r.Contains("TEACHER")) targetRoleName = "DOCENTE";
        else if (r.Contains("ADMIN")) targetRoleName = "ADMINISTRADOR";

        var rol = await _context.Roles.FirstOrDefaultAsync(ro => ro.Nombre == targetRoleName)
                  ?? await _context.Roles.FirstOrDefaultAsync();

        if (rol == null) return BadRequest(new { message = "No se encontró el rol en el sistema." });

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(string.IsNullOrWhiteSpace(dto.Password) ? "123456" : dto.Password);
        var avatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(cleanUsername)}";

        var usuario = new Usuario
        {
            RolId = rol.Id,
            Username = cleanUsername,
            PasswordHash = passwordHash,
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            AvatarUrl = avatarUrl,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        string? gradeResult = null;
        if (targetRoleName == "ALUMNO")
        {
            var rawGrade = dto.GradeLevel?.Trim() ?? "1°";
            var gradeKey = rawGrade.Contains('°') ? rawGrade.Substring(0, rawGrade.IndexOf('°') + 1) : rawGrade;
            var grado = await _context.Grados.FirstOrDefaultAsync(g => g.Nombre == gradeKey || g.Nombre == rawGrade)
                        ?? await _context.Grados.FirstOrDefaultAsync();

            if (grado != null)
            {
                _context.Alumnos.Add(new Alumno { UsuarioId = usuario.Id, GradoId = grado.Id });
                await _context.SaveChangesAsync();
                gradeResult = grado.Nombre;
            }
        }

        var result = new AdminUserDto
        {
            Id = usuario.Id,
            Username = usuario.Username,
            FullName = usuario.FullName,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Role = targetRoleName == "ALUMNO" ? "Student" : (targetRoleName == "DOCENTE" ? "Teacher" : "Admin"),
            GradeLevel = gradeResult,
            Activo = usuario.Activo,
            AvatarUrl = usuario.AvatarUrl,
            FechaCreacion = usuario.FechaCreacion
        };

        return CreatedAtAction(nameof(GetUsers), new { id = usuario.Id }, result);
    }

    /// <summary>
    /// Actualiza los datos de un usuario escolar existente.
    /// PUT: /api/admin/users/{id}
    /// </summary>
    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateAdminUserDto dto)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Alumno)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.Nombre)) usuario.Nombre = dto.Nombre.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Apellido)) usuario.Apellido = dto.Apellido.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        // Actualizar grado de alumno si aplica
        if (usuario.Alumno != null && !string.IsNullOrWhiteSpace(dto.GradeLevel))
        {
            var rawGrade = dto.GradeLevel.Trim();
            var gradeKey = rawGrade.Contains('°') ? rawGrade.Substring(0, rawGrade.IndexOf('°') + 1) : rawGrade;
            var grado = await _context.Grados.FirstOrDefaultAsync(g => g.Nombre == gradeKey || g.Nombre == rawGrade);
            if (grado != null)
            {
                usuario.Alumno.GradoId = grado.Id;
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Usuario ID {Id} actualizado por administrador {Admin}", id, User.Identity?.Name);

        return Ok(new { message = "Usuario actualizado exitosamente." });
    }

    /// <summary>
    /// Suspende / activa el acceso a un usuario sin eliminar sus datos históricos (Soft Delete).
    /// PATCH: /api/admin/users/{id}/toggle-status
    /// </summary>
    [HttpPatch("users/{id:int}/toggle-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleUserStatus(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

        // No permitir que el admin se inactive a sí mismo accidentalmente
        if (usuario.Username.Equals(User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "No puedes suspender tu propia cuenta de administrador en uso." });
        }

        usuario.Activo = !usuario.Activo;
        await _context.SaveChangesAsync();

        var statusText = usuario.Activo ? "habilitada" : "suspendida";
        _logger.LogWarning("Cuenta de usuario {Username} fue {StatusText} por administrador {Admin}", usuario.Username, statusText, User.Identity?.Name);

        return Ok(new
        {
            message = $"La cuenta de {usuario.FullName} ha sido {statusText}.",
            activo = usuario.Activo
        });
    }

    /// <summary>
    /// Consulta todas las materias/cursos registrados en la institución (CRUD Administrador).
    /// GET: /api/admin/courses
    /// </summary>
    [HttpGet("courses")]
    [ProducesResponseType(typeof(List<AdminCourseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminCourses()
    {
        var courses = await _context.Cursos
            .Include(c => c.Grado)
            .Include(c => c.Docente)
            .Include(c => c.Inscripciones.Where(i => i.Estado == "ACTIVA"))
            .OrderBy(c => c.Grado.Nombre)
            .ThenBy(c => c.Nombre)
            .Select(c => new AdminCourseDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Grado = c.Grado.Nombre,
                Grupo = c.Grupo,
                DocenteId = c.DocenteId,
                DocenteNombre = !string.IsNullOrWhiteSpace(c.Docente.FullName) ? c.Docente.FullName : c.Docente.Username,
                Descripcion = c.Descripcion,
                TotalStudents = c.Inscripciones.Count,
                FechaCreacion = c.FechaCreacion,
                Activo = c.Activo
            })
            .ToListAsync();

        return Ok(courses);
    }

    /// <summary>
    /// Crea una nueva materia/curso institucional y le asigna un docente.
    /// POST: /api/admin/courses
    /// </summary>
    [HttpPost("courses")]
    [ProducesResponseType(typeof(AdminCourseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAdminCourse([FromBody] CreateAdminCourseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { message = "El nombre de la materia es requerido." });

        var rawGrade = dto.Grado?.Trim() ?? "1°";
        var gradeKey = rawGrade.Contains('°') ? rawGrade.Substring(0, rawGrade.IndexOf('°') + 1) : rawGrade;
        var grado = await _context.Grados.FirstOrDefaultAsync(g => g.Nombre == gradeKey || g.Nombre == rawGrade)
                    ?? await _context.Grados.FirstOrDefaultAsync();

        if (grado == null)
        {
            grado = new Grado { Nombre = gradeKey, Descripcion = $"{gradeKey} de primaria" };
            _context.Grados.Add(grado);
            await _context.SaveChangesAsync();
        }

        Usuario? docente = null;
        if (dto.DocenteId.HasValue && dto.DocenteId.Value > 0)
        {
            docente = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == dto.DocenteId.Value && u.Rol.Nombre == "DOCENTE");
        }

        if (docente == null)
        {
            docente = await _context.Usuarios.FirstOrDefaultAsync(u => u.Rol.Nombre == "DOCENTE" && u.Activo)
                      ?? await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == User.Identity!.Name);
        }

        if (docente == null)
            return BadRequest(new { message = "No se encontró un docente para asignar a este curso." });

        var curso = new Curso
        {
            Nombre = dto.Nombre.Trim(),
            GradoId = grado.Id,
            Grupo = string.IsNullOrWhiteSpace(dto.Grupo) ? "A" : dto.Grupo.Trim().ToUpperInvariant(),
            DocenteId = docente.Id,
            Descripcion = dto.Descripcion?.Trim(),
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Cursos.Add(curso);
        await _context.SaveChangesAsync();

        var result = new AdminCourseDto
        {
            Id = curso.Id,
            Nombre = curso.Nombre,
            Grado = grado.Nombre,
            Grupo = curso.Grupo,
            DocenteId = docente.Id,
            DocenteNombre = !string.IsNullOrWhiteSpace(docente.FullName) ? docente.FullName : docente.Username,
            Descripcion = curso.Descripcion,
            TotalStudents = 0,
            FechaCreacion = curso.FechaCreacion,
            Activo = curso.Activo
        };

        return CreatedAtAction(nameof(GetAdminCourses), new { id = curso.Id }, result);
    }

    /// <summary>
    /// Actualiza la información de un curso institucional.
    /// PUT: /api/admin/courses/{id}
    /// </summary>
    [HttpPut("courses/{id:int}")]
    public async Task<IActionResult> UpdateAdminCourse(int id, [FromBody] UpdateAdminCourseDto dto)
    {
        var curso = await _context.Cursos.FindAsync(id);
        if (curso == null) return NotFound(new { message = "Curso no encontrado." });

        if (!string.IsNullOrWhiteSpace(dto.Nombre)) curso.Nombre = dto.Nombre.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Grupo)) curso.Grupo = dto.Grupo.Trim().ToUpperInvariant();
        curso.Descripcion = dto.Descripcion?.Trim();

        if (dto.DocenteId.HasValue && dto.DocenteId.Value > 0)
        {
            var docente = await _context.Usuarios.FindAsync(dto.DocenteId.Value);
            if (docente != null) curso.DocenteId = docente.Id;
        }

        if (!string.IsNullOrWhiteSpace(dto.Grado))
        {
            var rawGrade = dto.Grado.Trim();
            var gradeKey = rawGrade.Contains('°') ? rawGrade.Substring(0, rawGrade.IndexOf('°') + 1) : rawGrade;
            var grado = await _context.Grados.FirstOrDefaultAsync(g => g.Nombre == gradeKey || g.Nombre == rawGrade);
            if (grado != null) curso.GradoId = grado.Id;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Curso ID {Id} actualizado por administrador {Admin}", id, User.Identity?.Name);

        return Ok(new { message = "Curso actualizado correctamente." });
    }

    /// <summary>
    /// Activa o desactiva un curso institucional.
    /// PATCH: /api/admin/courses/{id}/toggle-status
    /// </summary>
    [HttpPatch("courses/{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleCourseStatus(int id)
    {
        var curso = await _context.Cursos.FindAsync(id);
        if (curso == null) return NotFound(new { message = "Curso no encontrado." });

        curso.Activo = !curso.Activo;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"El curso '{curso.Nombre}' ha sido {(curso.Activo ? "activado" : "desactivado")}.",
            activo = curso.Activo
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