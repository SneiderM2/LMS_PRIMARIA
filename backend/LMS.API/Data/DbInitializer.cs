using LMS.API.Configuration;
using LMS.API.Entities;
using LMS.API.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Data;

/// <summary>
/// Inicialización controlada de la base de datos.
///
/// Cambios de seguridad:
///  - Ya NO se crean cuentas con contraseñas conocidas (admin123, docente123…) de forma automática.
///    El sembrado de cuentas demo requiere Seed:Enabled=true y contraseñas explícitas que cumplan
///    la política institucional.
///  - Ya NO se reactivan ni se desbloquean cuentas en cada arranque.
///  - Ya NO se marca DataPolicyAccepted=true sin consentimiento real del titular.
///
/// Nota de calidad: la evolución del esquema debería migrarse a EF Core Migrations.
/// Se conserva la verificación idempotente para no romper el despliegue actual.
/// </summary>
public class DbInitializer
{
    private readonly LMSDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        LMSDbContext context,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<DbInitializer> logger)
    {
        _context = context;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await EnsureColumnsMigratedAsync();
        await SeedRolesAsync();
        await SeedGradesAsync();
        await SeedDemoUsersAsync();
        await WarnAboutDefaultAccountsAsync();
    }

    private async Task SeedRolesAsync()
    {
        if (await _context.Roles.AnyAsync())
        {
            return;
        }

        var roles = new List<Rol>
        {
            new() { Nombre = "ADMINISTRADOR", Descripcion = "Administra usuarios, cursos y configuración del LMS." },
            new() { Nombre = "DOCENTE", Descripcion = "Gestiona cursos, materiales, tareas y calificaciones." },
            new() { Nombre = "ALUMNO", Descripcion = "Consulta cursos, materiales y entrega actividades." }
        };

        await _context.Roles.AddRangeAsync(roles);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Roles base creados.");
    }

    private async Task SeedGradesAsync()
    {
        if (await _context.Grados.AnyAsync())
        {
            return;
        }

        var grados = new List<Grado>
        {
            new() { Nombre = "1°", Descripcion = "Primer grado de primaria." },
            new() { Nombre = "2°", Descripcion = "Segundo grado de primaria." },
            new() { Nombre = "3°", Descripcion = "Tercer grado de primaria." },
            new() { Nombre = "4°", Descripcion = "Cuarto grado de primaria." },
            new() { Nombre = "5°", Descripcion = "Quinto grado de primaria." }
        };

        await _context.Grados.AddRangeAsync(grados);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Grados base creados.");
    }

    private async Task SeedDemoUsersAsync()
    {
        if (!_configuration.GetValue<bool>("Seed:Enabled"))
        {
            _logger.LogInformation("Sembrado de cuentas demo deshabilitado (Seed:Enabled=false).");
            return;
        }

        if (!_environment.IsDevelopment() && !_configuration.GetValue<bool>("Seed:AllowInProduction"))
        {
            _logger.LogWarning("Seed:Enabled=true ignorado en producción. Habilite Seed:AllowInProduction solo para una instalación inicial controlada.");
            return;
        }

        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == "ADMINISTRADOR");
        var teacherRole = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == "DOCENTE");
        var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == "ALUMNO");

        if (adminRole == null || teacherRole == null || studentRole == null)
        {
            _logger.LogWarning("No se pudieron sembrar cuentas demo: faltan roles base.");
            return;
        }

        await EnsureDemoUserAsync("admin", "Administrador", "General", adminRole.Id,
            _configuration["Seed:AdminPassword"], "Administrador General");

        await EnsureDemoUserAsync("docente", "Profesor", "Primaria", teacherRole.Id,
            _configuration["Seed:TeacherPassword"], "Profesor Primaria");

        var student = await EnsureDemoUserAsync("estudiante", "Estudiante", "Primaria", studentRole.Id,
            _configuration["Seed:StudentPassword"], "Estudiante Primaria");

        if (student != null && student.Alumno == null)
        {
            var grado = await _context.Grados.FirstOrDefaultAsync(g => g.Nombre == "5°")
                        ?? await _context.Grados.FirstOrDefaultAsync();

            if (grado != null)
            {
                _context.Alumnos.Add(new Alumno { UsuarioId = student.Id, GradoId = grado.Id });
                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task<Usuario?> EnsureDemoUserAsync(
        string username,
        string nombre,
        string apellido,
        int roleId,
        string? password,
        string fullNameForValidation)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation(
                "Cuenta demo '{Username}' omitida: configure Seed:{Key}Password con una contraseña fuerte.",
                username,
                char.ToUpperInvariant(username[0]) + username[1..]);
            return null;
        }

        var validation = PasswordPolicy.Validate(password, username, fullNameForValidation);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Cuenta demo '{Username}' omitida: la contraseña configurada no cumple la política ({Reason}).",
                username,
                validation.Message);
            return null;
        }

        var existing = await _context.Usuarios
            .Include(u => u.Alumno)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username);

        if (existing != null)
        {
            // No se reactiva, desbloquea ni cambia la contraseña de cuentas existentes.
            return existing;
        }

        var user = new Usuario
        {
            Username = username,
            RolId = roleId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            Nombre = nombre,
            Apellido = apellido,
            AvatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(username)}",
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            DataPolicyAccepted = false,
            AccessFailedCount = 0
        };

        _context.Usuarios.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cuenta demo '{Username}' creada. Debe aceptar la política en el primer ingreso.", username);
        return user;
    }

    private async Task WarnAboutDefaultAccountsAsync()
    {
        var defaults = new[] { "admin", "docente", "estudiante" };

        var found = await _context.Usuarios
            .Where(u => defaults.Contains(u.Username.ToLower()))
            .Select(u => new { u.Username, u.DataPolicyAccepted })
            .ToListAsync();

        if (found.Count > 0)
        {
            _logger.LogWarning(
                "Cuentas con identificadores por defecto detectadas: {Accounts}. " +
                "Verifique que sus contraseñas hayan sido rotadas y que no estén activas en producción " +
                "(Ley 1581 de 2012 y lineamientos del MSPI).",
                string.Join(", ", found.Select(f => f.Username)));
        }
    }

    private async Task EnsureColumnsMigratedAsync()
    {
        try
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            // Marcador de esquema actualizado. Si existe, el esquema ya incluye las columnas nuevas.
            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = @"SELECT 1 FROM information_schema.columns
                                         WHERE table_schema = 'public' AND table_name = 'usuarios'
                                           AND column_name = 'anonymized_at' LIMIT 1;";

                var alreadyMigrated = await checkCmd.ExecuteScalarAsync();
                if (alreadyMigrated != null && alreadyMigrated != DBNull.Value)
                {
                    await EnsureLoginIndexAsync(conn);
                    await EnsureAuditLogsTableAsync(conn);
                    return;
                }
            }

            var tablesWithAutoIncrement = new[]
            {
                "roles", "grados", "usuarios", "cursos", "inscripciones",
                "tareas", "entregas", "archivos_entrega", "materiales"
            };

            foreach (var table in tablesWithAutoIncrement)
            {
                try
                {
                    using var seqCmd = conn.CreateCommand();
                    seqCmd.CommandText = $@"
                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{table}')
                        AND NOT EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = '{table}' AND column_name = 'id'
                            AND (column_default LIKE 'nextval%' OR is_identity = 'YES')
                        ) THEN
                            CREATE SEQUENCE IF NOT EXISTS {table}_id_seq;
                            PERFORM setval('{table}_id_seq', COALESCE((SELECT MAX(id) FROM {table}), 0) + 1, false);
                            ALTER TABLE {table} ALTER COLUMN id SET DEFAULT nextval('{table}_id_seq');
                            ALTER SEQUENCE {table}_id_seq OWNED BY {table}.id;
                        END IF;
                    END $$;";

                    await seqCmd.ExecuteNonQueryAsync();
                }
                catch (Exception seqEx)
                {
                    _logger.LogWarning(seqEx, "Secuencia '{Table}' no ajustada.", table);
                }
            }

            using (var boolCmd = conn.CreateCommand())
            {
                boolCmd.CommandText = @"
                DO $$
                DECLARE
                    tbl text;
                BEGIN
                    FOR tbl IN SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' LOOP
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = tbl AND column_name = 'activo'
                              AND data_type IN ('smallint', 'integer')
                        ) THEN
                            EXECUTE format('ALTER TABLE %I ALTER COLUMN activo DROP DEFAULT;', tbl);
                            EXECUTE format('ALTER TABLE %I ALTER COLUMN activo TYPE boolean USING (activo <> 0);', tbl);
                            EXECUTE format('ALTER TABLE %I ALTER COLUMN activo SET DEFAULT true;', tbl);
                        END IF;
                    END LOOP;
                END $$;";

                await boolCmd.ExecuteNonQueryAsync();
            }

            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'usuarios';";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existingColumns.Add(reader.GetString(0));
                }
            }

            var columnDefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "nombre", "VARCHAR(100) NOT NULL DEFAULT ''" },
                { "apellido", "VARCHAR(100) NOT NULL DEFAULT ''" },
                { "avatar_url", "VARCHAR(500) NULL" },
                { "data_policy_accepted", "BOOLEAN NOT NULL DEFAULT FALSE" },
                { "data_policy_accepted_at", "TIMESTAMP WITHOUT TIME ZONE NULL" },
                { "data_policy_version", "VARCHAR(20) NULL" },
                { "data_policy_evidence", "TEXT NULL" },
                { "access_failed_count", "INT NOT NULL DEFAULT 0" },
                { "lockout_end", "TIMESTAMP WITHOUT TIME ZONE NULL" },
                { "session_token", "VARCHAR(255) NULL" },
                { "last_login_at", "TIMESTAMP WITHOUT TIME ZONE NULL" },
                { "deleted_at", "TIMESTAMP WITHOUT TIME ZONE NULL" },
                { "anonymized_at", "TIMESTAMP WITHOUT TIME ZONE NULL" }
            };

            foreach (var col in columnDefs)
            {
                if (existingColumns.Contains(col.Key))
                {
                    continue;
                }

                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS \"{col.Key}\" {col.Value};";
                await alterCmd.ExecuteNonQueryAsync();
            }

            await EnsureLoginIndexAsync(conn);
            await EnsureAuditLogsTableAsync(conn);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Verificación de esquema no completada. Revise la conexión a PostgreSQL.");
        }
    }

    private async Task EnsureAuditLogsTableAsync(System.Data.Common.DbConnection conn)
    {
        try
        {
            using var auditCmd = conn.CreateCommand();
            auditCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS audit_logs (
                id BIGSERIAL PRIMARY KEY,
                timestamp TIMESTAMPTZ NOT NULL,
                level VARCHAR(20) NOT NULL,
                method VARCHAR(10) NOT NULL,
                path VARCHAR(500) NOT NULL,
                status_code INT NOT NULL,
                elapsed_ms BIGINT NOT NULL,
                ip VARCHAR(50) NOT NULL,
                user_id VARCHAR(100) NULL,
                user_email VARCHAR(200) NULL,
                user_role VARCHAR(50) NULL,
                action VARCHAR(250) NOT NULL,
                details TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_audit_logs_timestamp ON audit_logs (timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_audit_logs_level ON audit_logs (level);

            CREATE TABLE IF NOT EXISTS consentimientos_habeas_data (
                id SERIAL PRIMARY KEY,
                alumno_id INT NOT NULL REFERENCES alumnos(usuario_id) ON DELETE CASCADE,
                usuario_id INT NULL REFERENCES usuarios(id) ON DELETE SET NULL,
                acudiente_nombre VARCHAR(150) NOT NULL,
                acudiente_documento VARCHAR(50) NOT NULL,
                tipo_documento VARCHAR(20) NOT NULL DEFAULT 'CC',
                version_politica VARCHAR(32) NOT NULL,
                hash_texto_politica VARCHAR(128) NOT NULL,
                autoriza_servicio_educativo BOOLEAN NOT NULL,
                autoriza_datos_opcionales BOOLEAN NOT NULL DEFAULT FALSE,
                autoriza_transmision_internacional BOOLEAN NOT NULL DEFAULT FALSE,
                ip_origen VARCHAR(64) NOT NULL,
                user_agent VARCHAR(300) NOT NULL,
                fecha_autorizacion_utc TIMESTAMPTZ NOT NULL,
                fecha_revocatoria_utc TIMESTAMPTZ NULL
            );
            CREATE INDEX IF NOT EXISTS idx_consentimiento_alumno ON consentimientos_habeas_data (alumno_id);
            CREATE INDEX IF NOT EXISTS idx_consentimiento_alumno_vigente ON consentimientos_habeas_data (alumno_id, fecha_revocatoria_utc);

            CREATE TABLE IF NOT EXISTS solicitudes_arco (
                id SERIAL PRIMARY KEY,
                usuario_id INT NULL REFERENCES usuarios(id) ON DELETE SET NULL,
                alumno_id INT NULL REFERENCES alumnos(usuario_id) ON DELETE SET NULL,
                solicitante_nombre VARCHAR(150) NOT NULL,
                solicitante_documento VARCHAR(50) NOT NULL,
                tipo_solicitud VARCHAR(30) NOT NULL,
                descripcion VARCHAR(2000) NOT NULL,
                estado VARCHAR(30) NOT NULL DEFAULT 'RADICADA',
                fecha_solicitud_utc TIMESTAMPTZ NOT NULL,
                fecha_limite_legal_utc TIMESTAMPTZ NOT NULL,
                fecha_respuesta_utc TIMESTAMPTZ NULL,
                respuesta VARCHAR(2000) NULL,
                ip_origen VARCHAR(64) NULL
            );
            CREATE INDEX IF NOT EXISTS idx_solicitudes_arco_usuario ON solicitudes_arco (usuario_id);
            CREATE INDEX IF NOT EXISTS idx_solicitudes_arco_estado_limite ON solicitudes_arco (estado, fecha_limite_legal_utc);";
            await auditCmd.ExecuteNonQueryAsync();

            // La tabla no debe quedar expuesta por la Data API a roles anónimos.
            // El propietario de la tabla (usuario de la API) sigue pudiendo escribir.
            try
            {
                using var rlsCmd = conn.CreateCommand();
                rlsCmd.CommandText = "ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY;";
                await rlsCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No fue posible habilitar RLS en audit_logs. Revise el propietario de la tabla.");
            }
        }
        catch (Exception ex)
        {
            // La auditoría es un control de seguridad: un fallo no debe pasar inadvertido.
            _logger.LogError(ex, "No se pudo crear la tabla audit_logs. Revise permisos y esquema en PostgreSQL.");
        }
    }

    private async Task EnsureLoginIndexAsync(System.Data.Common.DbConnection conn)
    {
        try
        {
            // Detecta duplicados antes de imponer la unicidad para no romper el arranque.
            using (var dupCmd = conn.CreateCommand())
            {
                dupCmd.CommandText = @"SELECT COUNT(*) FROM (
                                          SELECT LOWER(username) AS key
                                          FROM usuarios
                                          GROUP BY LOWER(username)
                                          HAVING COUNT(*) > 1
                                      ) AS duplicados;";

                var duplicates = Convert.ToInt64(await dupCmd.ExecuteScalarAsync() ?? 0L);
                if (duplicates > 0)
                {
                    _logger.LogError(
                        "No se pudo crear el índice único de username: existen {Count} identificadores duplicados " +
                        "(comparación sin distinguir mayúsculas). Corrija los duplicados antes de reintentar.",
                        duplicates);
                    return;
                }
            }

            using var idxCmd = conn.CreateCommand();
            idxCmd.CommandText = @"
                DROP INDEX IF EXISTS idx_usuarios_lower_username;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_usuarios_lower_username ON usuarios (LOWER(username));";
            await idxCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // El índice es una garantía de integridad; si falla se registra para revisión.
            _logger.LogWarning(ex, "No fue posible crear el índice único de username en minúsculas.");
        }
    }
}
